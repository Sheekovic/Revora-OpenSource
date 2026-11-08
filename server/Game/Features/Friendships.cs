using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;
using Friend = Revora.Game.ConquerStructures.Society.Friend;
using Packet = Revora.Network.GamePackets.KnownPersons;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class Friendships
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private sealed class Invite { internal Guid Id; internal GameState Sender; internal Entity SenderEntity, ReceiverEntity; internal DateTime Expires; }
        private sealed class State { internal Entity Entity; internal DateTime LastRequest; internal readonly Dictionary<uint, Invite> Pending = new Dictionary<uint, Invite>(); }
        private static readonly ConditionalWeakTable<GameState, State> states = new ConditionalWeakTable<GameState, State>();
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_friend_consents(
id uuid PRIMARY KEY,requester bigint NOT NULL CHECK(requester>0),receiver bigint NOT NULL CHECK(receiver>0 AND receiver<>requester),
accepted_at timestamptz NOT NULL,removed_at timestamptz);
CREATE UNIQUE INDEX IF NOT EXISTS revora_friend_active_pair ON revora_friend_consents
(LEAST(requester,receiver),GREATEST(requester,receiver)) WHERE removed_at IS NULL;
DO $revora_index$ BEGIN IF to_regclass('revora_friend_owner_lookup') IS NULL THEN CREATE INDEX IF NOT EXISTS revora_friend_owner_lookup ON friends(entityid,friendid);
ELSIF NOT EXISTS (SELECT 1 FROM pg_index WHERE indexrelid=to_regclass('revora_friend_owner_lookup') AND indrelid='friends'::regclass AND indisvalid AND NOT indisunique AND indpred IS NULL AND indexprs IS NULL AND indkey::text=((SELECT attnum::text FROM pg_attribute WHERE attrelid='friends'::regclass AND attname='entityid' AND NOT attisdropped) || ' ' || (SELECT attnum::text FROM pg_attribute WHERE attrelid='friends'::regclass AND attname='friendid' AND NOT attisdropped))) THEN RAISE EXCEPTION 'Revora index definition mismatch: revora_friend_owner_lookup'; END IF; END $revora_index$;");
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
        { return Q.Player(p, false) && p.Account != null && p.Account.State == AccountTable.AccountState.Player && p.Friends != null; }
        internal static bool Frame(byte[] frame)
        {
            return frame != null && frame.Length >= 44 && frame.Length <= 128 && BitConverter.ToUInt16(frame, 0) == frame.Length - 8
                && BitConverter.ToUInt16(frame, 2) == 1019 && BitConverter.ToUInt64(frame, frame.Length - 8) == PacketHandler.ClientSeal;
        }
        internal static Packet Contact(GameState subject, byte type)
        {
            return new Packet(true) { UID = subject.Entity.UID, Type = type, Name = subject.Entity.Name, Online = true,
                NobilityRank = subject.Entity.NobilityRank, IsBoy = Flowers.IsBoy(subject.Entity.Body) };
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
            if (type == Packet.RemovePerson) { Remove(p, uid); return; }
            if (type != Packet.FriendApply && type != Packet.FriendAccept) return;
            GameState peer;
            if (!Kernel.GamePool.TryGetValue(uid, out peer) || !Ready(peer)) return;
            PairLocks(p, peer, () =>
            {
                if (!Ready(p) || !Ready(peer)) return;
                try
                {
                    if (type == Packet.FriendApply) Request(p, peer);
                    else Accept(p, peer);
                }
                catch (Exception ex) { Q.Fail(p, "friend-consent", ex); Q.Fail(peer, "friend-consent", ex); }
            });
        }
        private static void Request(GameState sender, GameState receiver)
        {
            DateTime now = Clock(); var source = For(sender); var target = For(receiver);
            if (source.LastRequest > now.AddSeconds(-1)) return;
            source.LastRequest = now;
            if (sender.Friends.ContainsKey(receiver.Entity.UID) && receiver.Friends.ContainsKey(sender.Entity.UID)) return;
            if (target.Pending.ContainsKey(sender.Entity.UID) || target.Pending.Count >= 16) return;
            target.Pending.Add(sender.Entity.UID, new Invite { Id = Guid.NewGuid(), Sender = sender, SenderEntity = sender.Entity,
                ReceiverEntity = receiver.Entity, Expires = now.AddMinutes(2) });
            receiver.Send(InvitationInfo(sender, receiver)); receiver.Send(Contact(sender, Packet.FriendApply));
            sender.Send("Your friend request has been sent.");
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
                    DateTime now = Clock(); if (invite.Expires <= now || !Ready(sender) || !Ready(receiver)) return;
                    foreach (var owner in new[] { first, second })
                    {
                        var other = ReferenceEquals(owner, first) ? second : first; long existing, total;
                        using (var q = Q.Command(c, t, "SELECT count(*) FROM friends WHERE entityid=@uid AND friendid=@peer", "uid", (long)owner.Entity.UID, "peer", (long)other.Entity.UID)) existing = Convert.ToInt64(q.ExecuteScalar());
                        if (existing > 1) throw new InvalidOperationException("Duplicate friend direction requires repair.");
                        using (var q = Q.Command(c, t, "SELECT count(DISTINCT friendid) FROM friends WHERE entityid=@uid", "uid", (long)owner.Entity.UID)) total = Convert.ToInt64(q.ExecuteScalar());
                        if (existing == 0 && total >= 50) { receiver.Send("One of your friend lists is full."); return; }
                        if (existing == 0 && Q.Execute(c, t, "INSERT INTO friends(entityid,friendid,friendname,message) VALUES(@uid,@peer,@name,'')",
                            "uid", (long)owner.Entity.UID, "peer", (long)other.Entity.UID, "name", other.Entity.Name) != 1)
                            throw new InvalidOperationException("Friend direction was not saved.");
                    }
                    using (var q = Q.Command(c, t, @"SELECT id FROM revora_friend_consents WHERE removed_at IS NULL
AND ((requester=@a AND receiver=@b) OR (requester=@b AND receiver=@a))", "a", (long)sender.Entity.UID, "b", (long)receiver.Entity.UID))
                        if (q.ExecuteScalar() != null) throw new InvalidOperationException("Active friend consent disagrees with the loaded list.");
                    if (Q.Execute(c, t, "INSERT INTO revora_friend_consents(id,requester,receiver,accepted_at) VALUES(@id,@a,@b,@now)",
                        "id", invite.Id, "a", (long)sender.Entity.UID, "b", (long)receiver.Entity.UID, "now", now) != 1)
                        throw new InvalidOperationException("Friend consent was not recorded.");
                    string key = invite.Id.ToString("N");
                    refreshFirst = ProgressionEvidence.Complete(c, t, first.Entity.UID, 10123, "mutual-friend-v1", key, 1);
                    refreshSecond = ProgressionEvidence.Complete(c, t, second.Entity.UID, 10123, "mutual-friend-v1", key, 1);
                    using (var q = Q.Command(c, t, "SELECT count(*) FROM revora_achievement_evidence WHERE achievement_id=10123 AND uid=ANY(@uids)",
                        "uids", new[] { (long)first.Entity.UID, (long)second.Entity.UID }))
                        if (Convert.ToInt64(q.ExecuteScalar()) != 2) throw new InvalidOperationException("Friend achievement was not saved.");
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
                            if (!owner) throw new InvalidOperationException("Friend owner missing.");
                            if (!Ready(p)) return;
                            long expected;
                            using (var q = Q.Command(c, t, "SELECT count(*) FROM friends WHERE (entityid=@a AND friendid=@b) OR (entityid=@b AND friendid=@a)", "a", (long)p.Entity.UID, "b", (long)uid)) expected = Convert.ToInt64(q.ExecuteScalar());
                            if (Q.Execute(c, t, "DELETE FROM friends WHERE (entityid=@a AND friendid=@b) OR (entityid=@b AND friendid=@a)", "a", (long)p.Entity.UID, "b", (long)uid) != expected) throw new InvalidOperationException("Friend removal was incomplete.");
                            object active;
                            using (var q = Q.Command(c, t, @"SELECT id FROM revora_friend_consents WHERE removed_at IS NULL
AND ((requester=@a AND receiver=@b) OR (requester=@b AND receiver=@a)) FOR UPDATE", "a", (long)p.Entity.UID, "b", (long)uid)) active = q.ExecuteScalar();
                            if (active != null && Q.Execute(c, t, "UPDATE revora_friend_consents SET removed_at=@now WHERE id=@id AND removed_at IS NULL", "now", Clock(), "id", active) != 1)
                                throw new InvalidOperationException("Friend removal was not recorded.");
                            t.Commit(); committed = true;
                        }
                    }
                }
                catch (Exception ex) { Q.Fail(p, "friend-remove", ex); if (peer != null && !peer.Fake) Q.Fail(peer, "friend-remove", ex); }
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
                var loaded = new SafeDictionary<uint, Friend>(50);
                using (var q = new MySqlCommand(MySqlCommandType.SELECT).Select("friends").Where("entityid", p.Entity.UID))
                using (var r = q.CreateReader()) while (r.Read())
                {
                    uint uid = checked((uint)r.ReadNullableInt64Strict("friendid").GetValueOrDefault());
                    if (uid == 0 || uid == p.Entity.UID || loaded.ContainsKey(uid)) throw new InvalidOperationException("Friend list requires repair.");
                    loaded.Add(uid, new Friend { ID = uid, Name = r.ReadString("friendname"), Message = r.ReadString("message") });
                }
                var before = p.Friends; p.Friends = loaded;
                if (!notify || !Ready(p)) return;
                if (before != null) foreach (uint uid in before.Keys.Where(i => !loaded.ContainsKey(i)).ToArray())
                    p.Send(new Packet(true) { UID = uid, Type = Packet.RemovePerson, Name = "", Online = false });
                foreach (var friend in loaded.Values.Where(i => before == null || !before.ContainsKey(i.ID)))
                {
                    GameState other;
                    p.Send(Kernel.GamePool.TryGetValue(friend.ID, out other) && Ready(other) ? Contact(other, Packet.FriendAccept)
                        : new Packet(true) { UID = friend.ID, Type = Packet.FriendAccept, Name = friend.Name, Online = false });
                }
            }
            catch (Exception ex) { Q.Fail(p, "friend-load", ex); throw; }
        }
    }
}
