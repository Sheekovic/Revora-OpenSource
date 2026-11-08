using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;
using Packet = Revora.Network.GamePackets.MentorApprentice;

namespace Revora.Game.Features
{
    internal static class MentorAdmissions
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private sealed class Invite { internal Guid Id; internal GameState Sender; internal Entity SenderEntity, ReceiverEntity; internal uint Type; internal DateTime Expires; }
        private sealed class State { internal Entity Entity; internal DateTime LastRequest; internal readonly Dictionary<uint, Invite> Pending = new Dictionary<uint, Invite>(); }
        private static readonly ConditionalWeakTable<GameState, State> states = new ConditionalWeakTable<GameState, State>();
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_mentor_admissions(
id uuid PRIMARY KEY,mentor bigint NOT NULL CHECK(mentor>0),apprentice bigint NOT NULL CHECK(apprentice>0 AND apprentice<>mentor),
requester bigint NOT NULL,accepted_at timestamptz NOT NULL,mentor_level integer NOT NULL,mentor_reborn integer NOT NULL,
apprentice_level integer NOT NULL,apprentice_reborn integer NOT NULL,capacity integer NOT NULL CHECK(capacity BETWEEN 2 AND 10));
CREATE INDEX IF NOT EXISTS revora_mentor_admission_child ON revora_mentor_admissions(apprentice,accepted_at);
DO $revora_index$ BEGIN IF to_regclass('revora_mentor_owner_lookup') IS NULL THEN CREATE INDEX IF NOT EXISTS revora_mentor_owner_lookup ON apprentice(mentorid,apprenticeid);
ELSIF NOT EXISTS (SELECT 1 FROM pg_index WHERE indexrelid=to_regclass('revora_mentor_owner_lookup') AND indrelid='apprentice'::regclass AND indisvalid AND NOT indisunique AND indpred IS NULL AND indexprs IS NULL AND indkey::text=((SELECT attnum::text FROM pg_attribute WHERE attrelid='apprentice'::regclass AND attname='mentorid' AND NOT attisdropped) || ' ' || (SELECT attnum::text FROM pg_attribute WHERE attrelid='apprentice'::regclass AND attname='apprenticeid' AND NOT attisdropped))) THEN RAISE EXCEPTION 'Revora index definition mismatch: revora_mentor_owner_lookup'; END IF; END $revora_index$;
DO $revora_index$ BEGIN IF to_regclass('revora_mentor_child_lookup') IS NULL THEN CREATE INDEX IF NOT EXISTS revora_mentor_child_lookup ON apprentice(apprenticeid);
ELSIF NOT EXISTS (SELECT 1 FROM pg_index WHERE indexrelid=to_regclass('revora_mentor_child_lookup') AND indrelid='apprentice'::regclass AND indisvalid AND NOT indisunique AND indpred IS NULL AND indexprs IS NULL AND indkey::text=((SELECT attnum::text FROM pg_attribute WHERE attrelid='apprentice'::regclass AND attname='apprenticeid' AND NOT attisdropped))) THEN RAISE EXCEPTION 'Revora index definition mismatch: revora_mentor_child_lookup'; END IF; END $revora_index$;");
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
        { return Q.Player(p) && p.Account != null && p.Account.State == AccountTable.AccountState.Player && p.Apprentices != null; }
        private static bool Near(GameState a, GameState b)
        { return ReferenceEquals(a.Map, b.Map) && a.Entity.MapID == b.Entity.MapID && Kernel.GetDistance(a.Entity.X, a.Entity.Y, b.Entity.X, b.Entity.Y) <= Constants.pScreenDistance; }
        internal static int Capacity(int level, int reborn, int first, int second)
        {
            if (reborn < 0 || reborn > 2 || level < 1 || level > Config.LevelCap || first < 0 || first > Config.LevelCap || second < 0 || second > Config.LevelCap) return 0;
            int peak = level;
            if (reborn >= 1) peak = Math.Max(peak, first); if (reborn == 2) peak = Math.Max(peak, second);
            return peak >= 134 ? 10 : peak >= 130 ? 7 : peak >= 120 ? 5 : peak >= 110 ? 3 : peak >= 70 ? 2 : 0;
        }
        internal static bool Eligible(int mentorLevel, int mentorReborn, int first, int second, int childLevel, int childReborn)
        {
            if (Capacity(mentorLevel, mentorReborn, first, second) == 0 || childLevel < 1 || childLevel > Config.LevelCap || childReborn < 0 || childReborn > 2) return false;
            return mentorReborn > childReborn || mentorReborn == childReborn && mentorLevel - childLevel >= 30 - mentorReborn * 10;
        }
        internal static bool Frame(byte[] p)
        {
            return p != null && p.Length >= 32 && p.Length <= 128 && BitConverter.ToUInt16(p, 0) == p.Length - 8
                && BitConverter.ToUInt16(p, 2) == 2065 && BitConverter.ToUInt64(p, p.Length - 8) == PacketHandler.ClientSeal
                && p[21] <= 16 && 22 + p[21] <= p.Length - 8;
        }
        internal static void Process(byte[] frame, GameState p)
        {
            if (!Frame(frame) || !Ready(p)) return;
            uint type = BitConverter.ToUInt32(frame, 4); bool request = type == Packet.RequestApprentice || type == Packet.RequestMentor;
            if (!request && type != Packet.AcceptRequestApprentice && type != Packet.AcceptRequestMentor) return;
            uint uid = BitConverter.ToUInt32(frame, request ? 12 : 8); GameState peer;
            if (uid == 0 || uid == p.Entity.UID || !Kernel.GamePool.TryGetValue(uid, out peer) || !Ready(peer)) return;
            var first = p.Entity.UID < peer.Entity.UID ? p : peer; var second = ReferenceEquals(first, p) ? peer : p;
            lock (first.ItemSyncRoot) lock (second.ItemSyncRoot) lock (MentorExperience.Sync)
            {
                if (!Ready(p) || !Ready(peer)) return;
                try
                {
                    if (request) Request(p, peer, type);
                    else
                    {
                        Invite invite;
                        if (!For(p).Pending.TryGetValue(uid, out invite) || invite.Type != type || !ReferenceEquals(invite.Sender, peer)
                            || !ReferenceEquals(invite.SenderEntity, peer.Entity) || !ReferenceEquals(invite.ReceiverEntity, p.Entity)) return;
                        if (frame[16] != 1) { For(p).Pending.Remove(uid); return; }
                        Accept(p, peer, invite);
                    }
                }
                catch (Exception ex) { Q.Fail(p, "mentor-admission", ex); Q.Fail(peer, "mentor-admission", ex); }
            }
        }
        private static void Request(GameState sender, GameState receiver, uint type)
        {
            if (!Near(sender, receiver)) return;
            DateTime now = Clock(); var source = For(sender); var target = For(receiver);
            if (source.LastRequest > now.AddSeconds(-1)) return; source.LastRequest = now;
            var mentor = type == Packet.RequestApprentice ? sender : receiver; var child = ReferenceEquals(mentor, sender) ? receiver : sender;
            if (child.Mentor != null || !Eligible(mentor.Entity.Level, mentor.Entity.Reborn, mentor.Entity.FirstRebornLevel, mentor.Entity.SecondRebornLevel, child.Entity.Level, child.Entity.Reborn)) return;
            if (target.Pending.ContainsKey(sender.Entity.UID) || target.Pending.Count >= 16) return;
            uint accept = type == Packet.RequestApprentice ? Packet.AcceptRequestApprentice : Packet.AcceptRequestMentor;
            target.Pending.Add(sender.Entity.UID, new Invite { Id = Guid.NewGuid(), Sender = sender, SenderEntity = sender.Entity, ReceiverEntity = receiver.Entity, Type = accept, Expires = now.AddMinutes(2) });
            receiver.Send(Friendships.InvitationInfo(sender, receiver));
            receiver.Send(new Packet(true) { Type = accept, UID = sender.Entity.UID, dwParam = receiver.Entity.UID,
                Dynamic = (byte)Math.Min(byte.MaxValue, sender.Entity.BattlePower), Online = true, Name = sender.Entity.Name });
            sender.Send("Your invitation has been sent.");
        }
        private static void Accept(GameState receiver, GameState sender, Invite invite)
        {
            var mentor = invite.Type == Packet.AcceptRequestApprentice ? sender : receiver;
            var child = ReferenceEquals(mentor, sender) ? receiver : sender;
            if (!Near(mentor, child) || child.Mentor != null) return;
            bool mentorAward, childAward;
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    var first = mentor.Entity.UID < child.Entity.UID ? mentor : child; var second = ReferenceEquals(first, mentor) ? child : mentor;
                    SkillTable.LockOwner(c, t, first); SkillTable.LockOwner(c, t, second);
                    DateTime now = Clock(); if (invite.Expires <= now || !Ready(mentor) || !Ready(child) || !Near(mentor, child)) return;
                    foreach (var player in new[] { first, second })
                    using (var q = Q.Command(c, t, "SELECT level,reborn,firstrebornlevel,secondrebornlevel FROM entities WHERE uid=@uid", "uid", (long)player.Entity.UID))
                    using (var r = q.ExecuteReader())
                    {
                        if (!r.Read() || Convert.ToInt32(r.GetValue(0)) != player.Entity.Level || Convert.ToInt32(r.GetValue(1)) != player.Entity.Reborn
                            || (!r.IsDBNull(2) ? Convert.ToInt32(r.GetValue(2)) : 0) != player.Entity.FirstRebornLevel
                            || (!r.IsDBNull(3) ? Convert.ToInt32(r.GetValue(3)) : 0) != player.Entity.SecondRebornLevel) return;
                    }
                    if (MentorDepartures.Cooling(c, t, child.Entity.UID)) { receiver.Send("This apprentice must wait before choosing another mentor."); return; }
                    int capacity = Capacity(mentor.Entity.Level, mentor.Entity.Reborn, mentor.Entity.FirstRebornLevel, mentor.Entity.SecondRebornLevel);
                    if (!Eligible(mentor.Entity.Level, mentor.Entity.Reborn, mentor.Entity.FirstRebornLevel, mentor.Entity.SecondRebornLevel, child.Entity.Level, child.Entity.Reborn)) return;
                    using (var q = Q.Command(c, t, "SELECT count(*) FROM apprentice WHERE apprenticeid=@uid", "uid", (long)child.Entity.UID))
                        if (Convert.ToInt64(q.ExecuteScalar()) != 0) return;
                    using (var q = Q.Command(c, t, @"SELECT (SELECT count(*) FROM apprentice WHERE apprenticeid=@uid)>1
OR EXISTS(SELECT 1 FROM apprentice a WHERE a.mentorid=@uid AND (SELECT count(*) FROM apprentice x WHERE x.apprenticeid=a.apprenticeid)<>1)", "uid", (long)mentor.Entity.UID))
                        if (Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Ambiguous mentor membership requires repair.");
                    using (var q = Q.Command(c, t, "SELECT apprenticeid FROM apprentice WHERE mentorid=@uid", "uid", (long)mentor.Entity.UID))
                    using (var r = q.ExecuteReader())
                    {
                        var ids = new HashSet<long>(); while (r.Read()) if (!ids.Add(r.GetInt64(0))) throw new InvalidOperationException("Duplicate apprentice membership requires repair.");
                        if (ids.Count >= capacity) { receiver.Send("This mentor cannot accept another apprentice yet."); return; }
                    }
                    using (var q = Q.Command(c, t, @"WITH RECURSIVE ancestors(uid) AS (
SELECT mentorid FROM apprentice WHERE apprenticeid=@mentor UNION SELECT a.mentorid FROM apprentice a JOIN ancestors x ON a.apprenticeid=x.uid)
SELECT EXISTS(SELECT 1 FROM ancestors WHERE uid=@child)", "mentor", (long)mentor.Entity.UID, "child", (long)child.Entity.UID))
                        if (Equals(q.ExecuteScalar(), true)) return;
                    DateTime local = now.ToLocalTime(); long enrolled = local.Year * 10000 + local.Month * 100 + local.Day;
                    if (Q.Execute(c, t, @"INSERT INTO apprentice(mentorid,apprenticeid,mentorname,apprenticename,enroledate,
actual_experience,total_experience,actual_plus,total_plus,actual_heavenblessing,total_heavenblessing)
VALUES(@mentor,@child,@mentorname,@childname,@date,0,0,0,0,0,0)", "mentor", (long)mentor.Entity.UID, "child", (long)child.Entity.UID,
                        "mentorname", mentor.Entity.Name, "childname", child.Entity.Name, "date", enrolled) != 1) throw new InvalidOperationException("Mentor admission was not saved.");
                    if (Q.Execute(c, t, @"INSERT INTO revora_mentor_admissions(id,mentor,apprentice,requester,accepted_at,mentor_level,mentor_reborn,apprentice_level,apprentice_reborn,capacity)
VALUES(@id,@mentor,@child,@requester,@now,@ml,@mr,@cl,@cr,@capacity)", "id", invite.Id, "mentor", (long)mentor.Entity.UID, "child", (long)child.Entity.UID,
                        "requester", (long)sender.Entity.UID, "now", now, "ml", (int)mentor.Entity.Level, "mr", (int)mentor.Entity.Reborn,
                        "cl", (int)child.Entity.Level, "cr", (int)child.Entity.Reborn, "capacity", capacity) != 1) throw new InvalidOperationException("Mentor consent was not saved.");
                    mentorAward = ProgressionEvidence.Complete(c, t, mentor.Entity.UID, 10124, "mentor-admission-v1", invite.Id.ToString("N"), 1);
                    childAward = ProgressionEvidence.Complete(c, t, child.Entity.UID, 10125, "mentor-admission-v1", invite.Id.ToString("N"), 1);
                    using (var q = Q.Command(c, t, "SELECT count(*) FROM revora_achievement_evidence WHERE (uid=@mentor AND achievement_id=10124) OR (uid=@child AND achievement_id=10125)",
                        "mentor", (long)mentor.Entity.UID, "child", (long)child.Entity.UID)) if (Convert.ToInt64(q.ExecuteScalar()) != 2) throw new InvalidOperationException("Mentor achievements were not saved.");
                    t.Commit();
                }
            }
            For(receiver).Pending.Remove(sender.Entity.UID); For(sender).Pending.Remove(receiver.Entity.UID);
            Database.KnownPersons.LoadMentor(mentor); Database.KnownPersons.LoadMentor(child);
            var app = new ApprenticeInformation { Apprentice_ID = child.Entity.UID, Apprentice_Level = child.Entity.Level, Apprentice_Name = child.Entity.Name,
                Apprentice_Class = child.Entity.Class, Apprentice_PkPoints = child.Entity.PKPoints, Apprentice_Online = true, Apprentice_Spouse_Name = child.Entity.Spouse,
                Enrole_date = child.Mentor.EnroleDate, Mentor_ID = mentor.Entity.UID, Mentor_Mesh = mentor.Entity.Mesh, Mentor_Name = mentor.Entity.Name, Type = 2 };
            var info = new MentorInformation(true) { Mentor_Type = 1, Mentor_ID = mentor.Entity.UID, Apprentice_ID = child.Entity.UID,
                Enrole_Date = child.Mentor.EnroleDate, Mentor_Level = mentor.Entity.Level, Mentor_Class = mentor.Entity.Class,
                Mentor_PkPoints = mentor.Entity.PKPoints, Mentor_Mesh = mentor.Entity.Mesh, Mentor_Online = true,
                Shared_Battle_Power = child.Entity.BattlePowerFrom(mentor.Entity), String_Count = 3, Mentor_Name = mentor.Entity.Name,
                Apprentice_Name = child.Entity.Name, Mentor_Spouse_Name = mentor.Entity.Spouse };
            mentor.Send(app); child.Send(info); child.ReviewMentor();
            if (mentorAward) Q.Refresh(mentor); if (childAward) Q.Refresh(child);
        }
    }
}
