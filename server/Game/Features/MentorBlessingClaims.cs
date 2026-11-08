using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class MentorBlessingClaims
    {
        private sealed class Row { internal uint Child; internal Guid? Departure; internal long Before, Taken; }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_mentor_blessing_claims(
id uuid PRIMARY KEY,mentor bigint NOT NULL CHECK(mentor>0),hours integer NOT NULL CHECK(hours BETWEEN 1 AND 65535),
seconds_before bigint NOT NULL CHECK(seconds_before BETWEEN 0 AND 4294967295),seconds_after bigint NOT NULL CHECK(seconds_after BETWEEN 0 AND 4294967295),
claimed_at timestamptz NOT NULL DEFAULT now(),CHECK(seconds_after=seconds_before+hours::bigint*3600));");
            }
        }
        internal static void Refresh(GameState p)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = Q.Command(c, null, @"SELECT LEAST(65535,COALESCE(sum(points),0)) FROM (
SELECT COALESCE(actual_heavenblessing,0)::numeric points FROM apprentice WHERE mentorid=@uid
UNION ALL SELECT actual_heavenblessing::numeric FROM revora_mentor_departures WHERE mentor=@uid)s", "uid", (long)p.Entity.UID))
                    p.PrizeHeavenBlessing = checked((ushort)Convert.ToDecimal(q.ExecuteScalar()));
            }
        }
        internal sealed class Contribution { internal uint Mentor; internal long Actual, Total; }
        internal static Contribution Credit(NpgsqlConnection c, NpgsqlTransaction t, GameState p, uint seconds)
        {
            if (!Monitor.IsEntered(p.ItemSyncRoot) || !Monitor.IsEntered(MentorExperience.Sync) || t == null)
                throw new InvalidOperationException("Blessing contribution requires its grant transaction and ownership.");
            long amount = seconds / 36000; // Preserve the existing Revora whole-hour contribution policy.
            if (amount == 0) return null;
            Contribution credit = null;
            using (var q = Q.Command(c, t, "SELECT mentorid,COALESCE(actual_heavenblessing,0),COALESCE(total_heavenblessing,0) FROM apprentice WHERE apprenticeid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
            using (var r = q.ExecuteReader())
            {
                if (r.Read())
                {
                    credit = new Contribution { Mentor = checked((uint)r.GetInt64(0)), Actual = r.GetInt64(1), Total = r.GetInt64(2) };
                    if (credit.Mentor == 0 || credit.Mentor == p.Entity.UID || credit.Actual < 0 || credit.Total < 0 || r.Read())
                        throw new InvalidOperationException("Ambiguous blessing contribution requires repair.");
                }
            }
            if (credit == null)
            {
                if (p.Mentor != null) throw new InvalidOperationException("Blessing membership changed.");
                return null;
            }
            if (p.Mentor == null || p.Mentor.ID != credit.Mentor) throw new InvalidOperationException("Blessing membership requires reload.");
            long actual = checked(credit.Actual + amount), total = checked(credit.Total + amount);
            if (Q.Execute(c, t, @"UPDATE apprentice SET actual_heavenblessing=@actual,total_heavenblessing=@total
WHERE mentorid=@mentor AND apprenticeid=@uid AND COALESCE(actual_heavenblessing,0)=@before AND COALESCE(total_heavenblessing,0)=@lifetime",
                "actual", actual, "total", total, "mentor", (long)credit.Mentor, "uid", (long)p.Entity.UID, "before", credit.Actual, "lifetime", credit.Total) != 1)
                throw new InvalidOperationException("Blessing contribution was not saved.");
            credit.Actual = actual; credit.Total = total; return credit;
        }
        internal static void ApplyCredit(GameState p, Contribution credit)
        {
            if (credit == null) return;
            if (!Monitor.IsEntered(p.ItemSyncRoot) || !Monitor.IsEntered(MentorExperience.Sync)) throw new InvalidOperationException("Blessing cache requires ownership.");
            ushort actual = (ushort)Math.Min(ushort.MaxValue, credit.Actual), total = (ushort)Math.Min(ushort.MaxValue, credit.Total);
            if (p.AsApprentice != null) { p.AsApprentice.Actual_HeavenBlessing = actual; p.AsApprentice.Total_HeavenBlessing = total; }
            GameState mentor;
            if (Kernel.GamePool.TryGetValue(credit.Mentor, out mentor) && Q.Player(mentor, false))
            {
                Game.ConquerStructures.Society.Apprentice app;
                if (mentor.Apprentices != null && mentor.Apprentices.TryGetValue(p.Entity.UID, out app))
                { app.Actual_HeavenBlessing = actual; app.Total_HeavenBlessing = total; }
                Refresh(mentor);
            }
        }
        internal static void Claim(GameState p)
        {
            if (!Monitor.IsEntered(p.ItemSyncRoot) || !Monitor.IsEntered(MentorExperience.Sync)) throw new InvalidOperationException("Mentor blessing claims require owner and contribution locks.");
            if (!Q.Player(p)) return;
            try
            {
                var rows = new List<Row>(); uint beforeSeconds = p.Entity.HeavenBlessing, next = beforeSeconds;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SkillTable.LockOwner(c, t, p); if (!Q.Player(p)) return;
                        long room = Math.Min(ushort.MaxValue, (uint.MaxValue - (long)beforeSeconds) / 3600);
                        if (room == 0) { p.Send("You have no room for more blessing time. Your reward is safe."); return; }
                        using (var q = Q.Command(c, t, "SELECT apprenticeid,COALESCE(actual_heavenblessing,0) FROM apprentice WHERE mentorid=@uid ORDER BY apprenticeid FOR UPDATE", "uid", (long)p.Entity.UID))
                        using (var r = q.ExecuteReader())
                        {
                            var seen = new HashSet<uint>();
                            while (r.Read())
                            {
                                uint child = checked((uint)r.GetInt64(0)); long before = r.GetInt64(1);
                                if (child == 0 || before < 0 || !seen.Add(child)) throw new InvalidOperationException("Ambiguous mentor contribution requires repair.");
                                rows.Add(new Row { Child = child, Before = before });
                            }
                        }
                        using (var q = Q.Command(c, t, "SELECT id,actual_heavenblessing FROM revora_mentor_departures WHERE mentor=@uid AND actual_heavenblessing>0 ORDER BY left_at,id LIMIT 100 FOR UPDATE", "uid", (long)p.Entity.UID))
                        using (var r = q.ExecuteReader()) while (r.Read()) rows.Add(new Row { Departure = r.GetGuid(0), Before = r.GetInt64(1) });
                        long available = 0; foreach (var row in rows) available = Math.Min(room, available + Math.Min(room, row.Before));
                        int count = (int)available; if (count == 0) return;
                        long remaining = count;
                        foreach (var row in rows)
                        {
                            row.Taken = Math.Min(remaining, row.Before); remaining -= row.Taken; if (row.Taken == 0) continue;
                            int changed;
                            if (row.Departure.HasValue) changed = Q.Execute(c, t, "UPDATE revora_mentor_departures SET actual_heavenblessing=actual_heavenblessing-@take WHERE id=@id AND mentor=@uid AND actual_heavenblessing=@before",
                                "take", row.Taken, "id", row.Departure.Value, "uid", (long)p.Entity.UID, "before", row.Before);
                            else changed = Q.Execute(c, t, "UPDATE apprentice SET actual_heavenblessing=actual_heavenblessing-@take WHERE mentorid=@uid AND apprenticeid=@child AND actual_heavenblessing=@before",
                                "take", row.Taken, "uid", (long)p.Entity.UID, "child", (long)row.Child, "before", row.Before);
                            if (changed != 1) throw new InvalidOperationException("Mentor blessing balance changed.");
                        }
                        next = checked(beforeSeconds + (uint)count * 3600);
                        BlessingBalance.Persist(c, t, p, next);
                        if (Q.Execute(c, t, "INSERT INTO revora_mentor_blessing_claims(id,mentor,hours,seconds_before,seconds_after) VALUES(@id,@uid,@hours,@before,@after)",
                            "id", Guid.NewGuid(), "uid", (long)p.Entity.UID, "hours", count, "before", (long)beforeSeconds, "after", (long)next) != 1)
                            throw new InvalidOperationException("Mentor blessing claim was not recorded.");
                        t.Commit();
                    }
                }
                BlessingBalance.ApplyCommitted(p, next);
                foreach (var row in rows.Where(r => !r.Departure.HasValue))
                {
                    ushort left = (ushort)Math.Min(ushort.MaxValue, row.Before - row.Taken);
                    Game.ConquerStructures.Society.Apprentice app;
                    if (p.Apprentices != null && p.Apprentices.TryGetValue(row.Child, out app)) app.Actual_HeavenBlessing = left;
                    GameState child;
                    if (Kernel.GamePool.TryGetValue(row.Child, out child) && child.Mentor != null && child.Mentor.ID == p.Entity.UID && child.AsApprentice != null)
                        child.AsApprentice.Actual_HeavenBlessing = left;
                }
                Refresh(p);
            }
            catch (Exception ex) { Q.Fail(p, "mentor-blessing-claim", ex); }
        }
    }
}
