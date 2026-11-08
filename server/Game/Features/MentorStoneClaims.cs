using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class MentorStoneClaims
    {
        private sealed class Row { internal uint Child; internal Guid? Departure; internal long Before, Taken; }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_mentor_stone_claims(
id uuid PRIMARY KEY,mentor bigint NOT NULL CHECK(mentor>0),points integer NOT NULL CHECK(points BETWEEN 100 AND 4000 AND points%100=0),
item_uids bigint[] NOT NULL,claimed_at timestamptz NOT NULL DEFAULT now(),CHECK(cardinality(item_uids)=points/100));");
            }
        }
        internal static void Refresh(GameState p)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = Q.Command(c, null, @"SELECT LEAST(65535,COALESCE(sum(points),0)) FROM (
SELECT COALESCE(actual_plus,0)::numeric points FROM apprentice WHERE mentorid=@uid
UNION ALL SELECT actual_plus::numeric FROM revora_mentor_departures WHERE mentor=@uid)s", "uid", (long)p.Entity.UID))
                    p.PrizePlusStone = checked((ushort)Convert.ToDecimal(q.ExecuteScalar()));
            }
        }
        internal static void Claim(GameState p)
        {
            if (!Monitor.IsEntered(p.ItemSyncRoot) || !Monitor.IsEntered(MentorExperience.Sync)) throw new InvalidOperationException("Mentor stone claims require owner and contribution locks.");
            if (!Q.Player(p)) return;
            try
            {
                var rows = new List<Row>(); var granted = new List<ConquerItem>();
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SkillTable.LockOwner(c, t, p); if (!Q.Player(p)) return;
                        int free;
                        using (var q = Q.Command(c, t, "SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0", "uid", (long)p.Entity.UID))
                        {
                            long saved = Convert.ToInt64(q.ExecuteScalar());
                            if (saved != p.Inventory.Count) throw new InvalidOperationException("Mentor reward inventory requires reload.");
                            free = (int)Math.Max(0, 40 - saved);
                        }
                        if (free == 0) { p.Send("Make room in your inventory for the stones."); return; }
                        using (var q = Q.Command(c, t, "SELECT apprenticeid,COALESCE(actual_plus,0) FROM apprentice WHERE mentorid=@uid ORDER BY apprenticeid FOR UPDATE", "uid", (long)p.Entity.UID))
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
                        using (var q = Q.Command(c, t, "SELECT id,actual_plus FROM revora_mentor_departures WHERE mentor=@uid AND actual_plus>0 ORDER BY left_at,id LIMIT 100 FOR UPDATE", "uid", (long)p.Entity.UID))
                        using (var r = q.ExecuteReader()) while (r.Read()) rows.Add(new Row { Departure = r.GetGuid(0), Before = r.GetInt64(1) });
                        long available = 0; foreach (var row in rows) available = Math.Min(free * 100L, available + Math.Min(free * 100L, row.Before));
                        int count = (int)(available / 100); if (count == 0) return;
                        long remaining = count * 100L;
                        foreach (var row in rows)
                        {
                            row.Taken = Math.Min(remaining, row.Before); remaining -= row.Taken; if (row.Taken == 0) continue;
                            int changed;
                            if (row.Departure.HasValue) changed = Q.Execute(c, t, "UPDATE revora_mentor_departures SET actual_plus=actual_plus-@take WHERE id=@id AND mentor=@uid AND actual_plus=@before",
                                "take", row.Taken, "id", row.Departure.Value, "uid", (long)p.Entity.UID, "before", row.Before);
                            else changed = Q.Execute(c, t, "UPDATE apprentice SET actual_plus=actual_plus-@take WHERE mentorid=@uid AND apprenticeid=@child AND actual_plus=@before",
                                "take", row.Taken, "uid", (long)p.Entity.UID, "child", (long)row.Child, "before", row.Before);
                            if (changed != 1) throw new InvalidOperationException("Mentor stone balance changed.");
                        }
                        for (int n = 0; n < count; n++)
                        {
                            var item = Q.New(730001, false); item.Plus = 1;
                            if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability)
VALUES(@item,730001,@uid,0,0,1,0,1,@dur,@dur)", "item", (long)item.UID, "uid", (long)p.Entity.UID, "dur", (int)item.MaximDurability) != 1)
                                throw new InvalidOperationException("Mentor stone was not saved.");
                            granted.Add(item);
                        }
                        if (Q.Execute(c, t, "INSERT INTO revora_mentor_stone_claims(id,mentor,points,item_uids) VALUES(@id,@uid,@points,@items)", "id", Guid.NewGuid(),
                            "uid", (long)p.Entity.UID, "points", count * 100, "items", granted.Select(i => (long)i.UID).ToArray()) != 1)
                            throw new InvalidOperationException("Mentor stone claim was not recorded.");
                        t.Commit();
                    }
                }
                Q.Install(p, new uint[0], granted);
                foreach (var row in rows.Where(r => !r.Departure.HasValue))
                {
                    ushort left = (ushort)Math.Min(ushort.MaxValue, row.Before - row.Taken);
                    Game.ConquerStructures.Society.Apprentice app;
                    if (p.Apprentices != null && p.Apprentices.TryGetValue(row.Child, out app)) app.Actual_Plus = left;
                    GameState child;
                    if (Kernel.GamePool.TryGetValue(row.Child, out child) && child.Mentor != null && child.Mentor.ID == p.Entity.UID && child.AsApprentice != null)
                        child.AsApprentice.Actual_Plus = left;
                }
                Refresh(p);
            }
            catch (Exception ex) { Q.Fail(p, "mentor-stone-claim", ex); }
        }
    }
}
