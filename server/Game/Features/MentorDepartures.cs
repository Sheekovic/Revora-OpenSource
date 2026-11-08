using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;
using Packet = Revora.Network.GamePackets.MentorApprentice;

namespace Revora.Game.Features
{
    internal static class MentorDepartures
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_mentor_departures(
id uuid PRIMARY KEY,mentor bigint NOT NULL CHECK(mentor>0),apprentice bigint NOT NULL CHECK(apprentice>0 AND apprentice<>mentor),
actor bigint NOT NULL,reason text NOT NULL CHECK(reason IN('leave','expel')),left_at timestamptz NOT NULL,can_join_at timestamptz NOT NULL CHECK(can_join_at>left_at),
original_row jsonb NOT NULL,actual_experience bigint NOT NULL CHECK(actual_experience>=0),total_experience bigint NOT NULL CHECK(total_experience>=0),
actual_plus bigint NOT NULL CHECK(actual_plus>=0),total_plus bigint NOT NULL CHECK(total_plus>=0),
actual_heavenblessing bigint NOT NULL CHECK(actual_heavenblessing>=0),total_heavenblessing bigint NOT NULL CHECK(total_heavenblessing>=0),
original_fee bigint NOT NULL CHECK(original_fee IN(0,50000)),fee_gold bigint NOT NULL CHECK(fee_gold BETWEEN 0 AND original_fee));
CREATE INDEX IF NOT EXISTS revora_mentor_departure_child ON revora_mentor_departures(apprentice,can_join_at);
CREATE INDEX IF NOT EXISTS revora_mentor_departure_owner ON revora_mentor_departures(mentor,left_at,id);");
            }
        }
        internal static bool Cooling(NpgsqlConnection c, NpgsqlTransaction t, uint child)
        {
            using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM revora_mentor_departures WHERE apprentice=@uid AND can_join_at>@now)", "uid", (long)child, "now", Clock()))
                return Equals(q.ExecuteScalar(), true);
        }
        internal static ulong Experience(uint mentor)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = Q.Command(c, null, "SELECT LEAST(30300,COALESCE(sum(actual_experience),0))::bigint FROM revora_mentor_departures WHERE mentor=@uid", "uid", (long)mentor))
                    return checked((ulong)Convert.ToInt64(q.ExecuteScalar()));
            }
        }
        internal static void Process(byte[] frame, GameState p)
        {
            if (!MentorAdmissions.Frame(frame) || !Q.Player(p)) return;
            uint type = System.BitConverter.ToUInt32(frame, 4); bool leave = type == Packet.LeaveMentor;
            if (!leave && type != Packet.ExpellApprentice) return;
            uint child = leave ? p.Entity.UID : System.BitConverter.ToUInt32(frame, 12);
            uint other = leave ? (p.Mentor == null ? 0 : p.Mentor.ID) : child;
            if (child == 0 || other == 0 || other == p.Entity.UID) return;
            GameState peer; if (!Kernel.GamePool.TryGetValue(other, out peer) || peer.Entity == null || peer.ItemSyncRoot == null) peer = null;
            bool committed = false; uint mentor = leave ? other : p.Entity.UID;
            Action owned = () =>
            {
                lock (MentorExperience.Sync)
                {
                    if (!Q.Player(p)) return;
                    try
                    {
                        ulong money = p.Entity.Money; ulong fee = leave ? 50000UL : 0;
                        if (money < fee) { p.Send("You need 50,000 silver to leave your mentor."); return; }
                        using (var c = DataHolder.MySqlConnection)
                        {
                            c.Open(); using (var t = c.BeginTransaction())
                            {
                                bool actorExists = false;
                                using (var q = Q.Command(c, t, "SELECT uid FROM entities WHERE uid=ANY(@uids) ORDER BY uid FOR UPDATE", "uids", new[] { (long)p.Entity.UID, (long)other }))
                                using (var r = q.ExecuteReader()) while (r.Read()) if (r.GetInt64(0) == p.Entity.UID) actorExists = true;
                                if (!actorExists || !Q.Player(p)) return;
                                using (var q = Q.Command(c, t, "SELECT mentorid FROM apprentice WHERE apprenticeid=@child FOR UPDATE", "child", (long)child))
                                using (var r = q.ExecuteReader())
                                {
                                    if (!r.Read()) return; long savedMentor = r.GetInt64(0);
                                    if (r.Read()) throw new InvalidOperationException("Ambiguous mentor departure requires repair.");
                                    if (savedMentor != mentor) return;
                                }
                                DateTime now = Clock(); Guid id = Guid.NewGuid();
                                if (Q.Execute(c, t, @"INSERT INTO revora_mentor_departures(id,mentor,apprentice,actor,reason,left_at,can_join_at,original_row,
actual_experience,total_experience,actual_plus,total_plus,actual_heavenblessing,total_heavenblessing,original_fee,fee_gold)
SELECT @id,mentorid,apprenticeid,@actor,@reason,@now,@until,to_jsonb(a),COALESCE(actual_experience,0),COALESCE(total_experience,0),
COALESCE(actual_plus,0),COALESCE(total_plus,0),COALESCE(actual_heavenblessing,0),COALESCE(total_heavenblessing,0),@fee,@fee
FROM apprentice a WHERE apprenticeid=@child AND mentorid=@mentor", "id", id, "actor", (long)p.Entity.UID, "reason", leave ? "leave" : "expel",
                                    "now", now, "until", now.AddDays(3), "fee", (long)fee, "child", (long)child, "mentor", (long)mentor) != 1)
                                    throw new InvalidOperationException("Mentor departure was not retained.");
                                if (fee > 0) SilverBalance.Persist(c, t, p, money - fee);
                                if (Q.Execute(c, t, "DELETE FROM apprentice WHERE apprenticeid=@child AND mentorid=@mentor", "child", (long)child, "mentor", (long)mentor) != 1)
                                    throw new InvalidOperationException("Mentor membership was not removed.");
                                t.Commit();
                            }
                        }
                        committed = true;
                        if (fee > 0) SilverBalance.ApplyCommitted(p, money - fee);
                        // Contribution writers also hold Sync. Replace these owned
                        // snapshots before allowing a new EXP/blessing operation.
                        Database.KnownPersons.LoadMentor(p);
                        if (peer != null && !peer.Fake) Database.KnownPersons.LoadMentor(peer);
                        if (p.Mentor == null) p.Entity.MentorBattlePower = 0;
                        if (peer != null && !peer.Fake && peer.Mentor == null) peer.Entity.MentorBattlePower = 0;
                    }
                    catch (Exception ex) { Q.Fail(p, "mentor-departure", ex); if (peer != null && !peer.Fake) Q.Fail(peer, "mentor-departure", ex); }
                }
            };
            if (peer == null) { lock (p.ItemSyncRoot) owned(); }
            else
            {
                var first = p.Entity.UID < peer.Entity.UID ? p : peer; var second = ReferenceEquals(first, p) ? peer : p;
                lock (first.ItemSyncRoot) lock (second.ItemSyncRoot) owned();
            }
            if (!committed) return;
            // Resolve the current sessions after commit, including login during offline departure.
            RefreshOnline(mentor, child, true, p.Entity.UID, other); RefreshOnline(child, mentor, false, p.Entity.UID, other);
            GameState recipient; if (Kernel.GamePool.TryGetValue(mentor, out recipient)) ClaimFees(recipient);
            p.Send("The apprenticeship has ended. A new mentor may be chosen in three days.");
        }
        private static void RefreshOnline(uint uid, uint other, bool mentor, uint actor, uint target)
        {
            GameState p; if (!Kernel.GamePool.TryGetValue(uid, out p) || p.Fake || p.Entity == null || p.ItemSyncRoot == null) return;
            lock (p.ItemSyncRoot) lock (MentorExperience.Sync)
            {
                try
                {
                Database.KnownPersons.LoadMentor(p);
                if (!Q.Player(p, false)) return;
                if (mentor ? !p.Apprentices.ContainsKey(other) : p.Mentor == null)
                    p.Send(new Packet(true) { Type = mentor ? Packet.DumpApprentice : Packet.DumpMentor, UID = actor, dwParam = target, Online = false });
                if (mentor && !p.Apprentices.ContainsKey(other)) p.Send(new ApprenticeInformation { Apprentice_ID = other, Mentor_ID = uid,
                    Mentor_Mesh = p.Entity.Mesh, Mentor_Name = p.Entity.Name, Apprentice_Name = "", Apprentice_Spouse_Name = "", Type = 2 });
                p.ReviewMentor();
                }
                catch (Exception ex) { Q.Fail(p, "mentor-departure-refresh", ex); }
            }
        }
        internal static void ClaimFees(GameState p)
        {
            if (!Q.Player(p, false)) return;
            lock (p.ItemSyncRoot) lock (MentorExperience.Sync)
            {
                if (!Q.Player(p, false)) return;
                try
                {
                    ulong money = p.Entity.Money, gain = 0; var ids = new List<Guid>();
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction())
                        {
                            SkillTable.LockOwner(c, t, p); if (!Q.Player(p, false)) return;
                            using (var q = Q.Command(c, t, "SELECT id,fee_gold FROM revora_mentor_departures WHERE mentor=@uid AND fee_gold>0 ORDER BY left_at,id LIMIT 64 FOR UPDATE", "uid", (long)p.Entity.UID))
                            using (var r = q.ExecuteReader()) while (r.Read())
                            {
                                ulong amount = checked((ulong)r.GetInt64(1)); if (amount > Entity.MaximumSilver - money - gain) continue;
                                gain += amount; ids.Add(r.GetGuid(0));
                            }
                            if (gain == 0) return;
                            foreach (var id in ids) if (Q.Execute(c, t, "UPDATE revora_mentor_departures SET fee_gold=0 WHERE id=@id AND mentor=@uid AND fee_gold>0", "id", id, "uid", (long)p.Entity.UID) != 1)
                                throw new InvalidOperationException("Mentor fee was not consumed.");
                            SilverBalance.Persist(c, t, p, money + gain); t.Commit();
                        }
                    }
                    SilverBalance.ApplyCommitted(p, money + gain);
                }
                catch (Exception ex) { Q.Fail(p, "mentor-departure-fee", ex); }
            }
        }
    }
}
