using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class DisCityRanking
    {
        private const uint Reward = 3008058;
        private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private static bool enabled;
        internal static string Day(DateTime utc)
        {
            if (utc.Kind != DateTimeKind.Utc) throw new ArgumentException("Dis City finish time requires UTC.");
            return TimeZoneInfo.ConvertTimeFromUtc(utc, Zone).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }
        private static void LockDay(NpgsqlConnection c, NpgsqlTransaction t, string day)
        {
            using (var q = Q.Command(c, t, "SELECT pg_advisory_xact_lock(67116475,@day)",
                "day", int.Parse(day, CultureInfo.InvariantCulture))) q.ExecuteNonQuery();
        }
        internal static void Initialize()
        {
            if (!ConquerItemInformation.BaseInformations.ContainsKey(Reward)) throw new InvalidOperationException("Dis City rank prize missing.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using(var t=c.BeginTransaction()) { Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_dis_finishes(
run uuid PRIMARY KEY, uid bigint NOT NULL, name varchar(32) NOT NULL, day varchar(8) NOT NULL,
started_at timestamptz NOT NULL, finished_at timestamptz NOT NULL, elapsed_ticks bigint NOT NULL CHECK(elapsed_ticks>=0),
finishing_step integer NOT NULL, UNIQUE(uid,day), FOREIGN KEY(run,finishing_step) REFERENCES revora_dis_steps(run,step));
CREATE INDEX IF NOT EXISTS revora_dis_finish_order ON revora_dis_finishes(day,elapsed_ticks,finished_at,uid);
CREATE TABLE IF NOT EXISTS revora_dis_rank_days(day varchar(8) PRIMARY KEY, settled_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_dis_rank_prizes(
day varchar(8) NOT NULL REFERENCES revora_dis_rank_days(day), rank integer NOT NULL CHECK(rank BETWEEN 1 AND 10),
uid bigint NOT NULL, run uuid NOT NULL REFERENCES revora_dis_finishes(run), name varchar(32) NOT NULL,
elapsed_ticks bigint NOT NULL CHECK(elapsed_ticks>=0), item_type integer NOT NULL, quantity integer NOT NULL CHECK(quantity BETWEEN 1 AND 3),
claimed_at timestamptz, rewards bigint[], PRIMARY KEY(day,rank), UNIQUE(day,uid), UNIQUE(run),
CHECK((claimed_at IS NULL)=(rewards IS NULL) AND (rewards IS NULL OR cardinality(rewards)=quantity)));
CREATE INDEX IF NOT EXISTS revora_dis_rank_pending ON revora_dis_rank_prizes(uid,day) WHERE claimed_at IS NULL;
ALTER TABLE revora_dis_finishes ADD COLUMN IF NOT EXISTS difficulty integer NOT NULL DEFAULT 0 CHECK(difficulty BETWEEN 0 AND 2);
ALTER TABLE revora_dis_finishes ADD COLUMN IF NOT EXISTS revision integer NOT NULL DEFAULT 1 CHECK(revision>0);
ALTER TABLE revora_dis_rank_prizes ADD COLUMN IF NOT EXISTS difficulty integer NOT NULL DEFAULT 0 CHECK(difficulty BETWEEN 0 AND 2);
ALTER TABLE revora_dis_rank_prizes ADD COLUMN IF NOT EXISTS revision integer NOT NULL DEFAULT 1 CHECK(revision>0);
ALTER TABLE revora_dis_rank_prizes DROP CONSTRAINT IF EXISTS revora_dis_rank_prizes_pkey;
ALTER TABLE revora_dis_rank_prizes ADD PRIMARY KEY(day,difficulty,revision,rank);"); t.Commit(); }
            }
            enabled = true;
        }
        internal static void Record(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Guid run, int step, DateTime started)
        {
            if (!enabled || t == null || !Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot) || started.Kind != DateTimeKind.Utc)
                throw new InvalidOperationException("Dis City finish requires owned victory transaction.");
            // Sample again after acquiring the day lock: a request waiting across
            // midnight belongs to the day on which its finish is accepted.
            DateTime now; string day; int attempts = 0;
            while (true)
            {
                if (++attempts > 3) throw new InvalidOperationException("Dis City clock did not stabilize.");
                now = DisCityQuest.Clock(); day = Day(now); LockDay(c, t, day);
                now = DisCityQuest.Clock(); if (Day(now) == day) break;
            }
            if (now < started) throw new InvalidOperationException("Dis City clock moved before admission.");
            if (Q.Execute(c, t, @"INSERT INTO revora_dis_finishes(run,uid,name,day,started_at,finished_at,elapsed_ticks,finishing_step,difficulty,revision)
SELECT @run,@uid,@name,@day,@started,@now,@elapsed,@step,p.difficulty,p.revision
FROM revora_dis_run_rules p WHERE p.run=@run AND p.uid=@uid AND EXISTS(SELECT 1 FROM revora_dis_runs r JOIN revora_dis_steps s ON s.run=r.run AND s.uid=r.uid
WHERE r.uid=@uid AND r.run=@run AND r.stage=4 AND r.phase='victory' AND s.step=@step AND s.stage=4 AND s.action='defeat' AND s.monster_uid<>0)
AND NOT EXISTS(SELECT 1 FROM revora_dis_rank_days WHERE day=@day)", "run", run, "uid", (long)p.Entity.UID,
                "name", p.Entity.Name, "day", day, "started", started, "now", now, "elapsed", (now - started).Ticks, "step", step) != 1)
                throw new InvalidOperationException("Dis City finish source or day changed.");
        }
        private static void Settle(NpgsqlConnection c, NpgsqlTransaction t, string day)
        {
            LockDay(c, t, day); DateTime now = DisCityQuest.Clock();
            if (string.CompareOrdinal(day, Day(now)) >= 0) throw new InvalidOperationException("Dis City ranking day is still open.");
            // The day lock also serializes accepted finishes. A missing write is
            // not evidence that another request already settled this day.
            using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM revora_dis_rank_days WHERE day=@day)", "day", day))
                if (Equals(q.ExecuteScalar(), true)) return;
            int expected;
            using (var q = Q.Command(c, t, "SELECT count(*) FROM (SELECT row_number() OVER(PARTITION BY difficulty,revision ORDER BY elapsed_ticks,finished_at,uid) place FROM revora_dis_finishes WHERE day=@day) winners WHERE place<=10", "day", day))
                expected = Convert.ToInt32(q.ExecuteScalar());
            if (Q.Execute(c, t, "INSERT INTO revora_dis_rank_days(day,settled_at) VALUES(@day,@now)",
                "day", day, "now", now) != 1)
                throw new InvalidOperationException("Dis City ranking day was not saved.");
            int written = Q.Execute(c, t, @"INSERT INTO revora_dis_rank_prizes(day,rank,uid,run,name,elapsed_ticks,item_type,quantity,difficulty,revision)
SELECT day,place,uid,run,name,elapsed_ticks,@item,CASE WHEN place=1 THEN 3 WHEN place=2 THEN 2 ELSE 1 END,difficulty,revision
FROM (SELECT day,uid,run,name,elapsed_ticks,difficulty,revision,row_number() OVER(PARTITION BY difficulty,revision ORDER BY elapsed_ticks,finished_at,uid) AS place
FROM revora_dis_finishes WHERE day=@day) winners WHERE place<=10",
                "day", day, "item", (int)Reward);
            if (written != expected)
                throw new InvalidOperationException("Dis City ranking winners were not all saved.");
        }
        private static string Yesterday()
        { return TimeZoneInfo.ConvertTimeFromUtc(DisCityQuest.Clock(), Zone).Date.AddDays(-1).ToString("yyyyMMdd", CultureInfo.InvariantCulture); }
        private static string Board(GameState p)
        {
            string day = Yesterday(); var leaders = new List<string>(); int own = 0;
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    Settle(c, t, day);
                    using (var q = Q.Command(c, t, "SELECT rank,uid,name,elapsed_ticks,difficulty,revision FROM revora_dis_rank_prizes WHERE day=@day ORDER BY difficulty,revision,rank", "day", day))
                    using (var r = q.ExecuteReader()) while (r.Read())
                    {
                        int rank = r.GetInt32(0); if (Convert.ToUInt32(r[1]) == p.Entity.UID) own = rank;
                        if (rank <= 3) leaders.Add(DisCityRunRules.Name(r.GetInt32(4)) + " v" + r.GetInt32(5) + " " + rank + ". " + r.GetString(2) + " (" + TimeSpan.FromTicks(r.GetInt64(3)).TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + "s)");
                    }
                    t.Commit();
                }
            }
            if (leaders.Count == 0) return "No hero defeated Pluto yesterday. Earlier unclaimed honors still await their owners.";
            return "Yesterday's swiftest: " + string.Join("; ", leaders) + ". "
                + (own == 0 ? "The fastest ten in each trial earn Sacred Refinery Packs." : "You placed " + own + ". Your prize awaits if unclaimed.");
        }
        private static string Claim(GameState p)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    SkillTable.LockOwner(c, t, p);
                    var days = new List<string>();
                    using (var q = Q.Command(c, t, @"SELECT f.day FROM revora_dis_finishes f
WHERE f.uid=@uid AND f.day<@today AND NOT EXISTS(SELECT 1 FROM revora_dis_rank_days d WHERE d.day=f.day)
ORDER BY f.day LIMIT 16", "uid", (long)p.Entity.UID, "today", Day(DisCityQuest.Clock())))
                    using (var r = q.ExecuteReader()) while (r.Read()) days.Add(r.GetString(0));
                    foreach (string day in days) Settle(c, t, day);
                    string selected = null; int rank = 0, quantity = 0; uint item = 0;
                    using (var q = Q.Command(c, t, @"SELECT day,rank,item_type,quantity FROM revora_dis_rank_prizes
WHERE uid=@uid AND claimed_at IS NULL AND day<@today ORDER BY day LIMIT 1 FOR UPDATE",
                        "uid", (long)p.Entity.UID, "today", Day(DisCityQuest.Clock())))
                    using (var r = q.ExecuteReader()) if (r.Read())
                    { selected = r.GetString(0); rank = r.GetInt32(1); item = Convert.ToUInt32(r[2]); quantity = r.GetInt32(3); }
                    if (selected == null)
                    {
                        t.Commit(); return days.Count == 16 ? "I have reviewed part of your old record. Speak to me again for the remaining days."
                            : "No unclaimed rank prize bears your name. Today's victories are judged tomorrow.";
                    }
                    if (p.Inventory.Count + quantity > 40) { t.Commit(); return "Make " + quantity + " empty places. Your rank prize will wait."; }
                    if (item != Reward || quantity != (rank == 1 ? 3 : rank == 2 ? 2 : 1)) throw new InvalidOperationException("Dis City rank prize differs from its edition.");
                    var rewards = Enumerable.Range(0, quantity).Select(n => Q.New(item)).ToArray();
                    foreach (var reward in rewards) Q.Insert(c, t, p.Entity.UID, reward);
                    if (Q.Execute(c, t, @"UPDATE revora_dis_rank_prizes SET claimed_at=@now,rewards=@rewards
WHERE day=@day AND uid=@uid AND claimed_at IS NULL", "now", DisCityQuest.Clock(), "day", selected,
                        "uid", (long)p.Entity.UID, "rewards", rewards.Select(i => (long)i.UID).ToArray()) != 1)
                        throw new InvalidOperationException("Dis City rank prize changed.");
                    t.Commit(); Q.Install(p, new uint[0], rewards);
                    return "Your place among the swiftest was " + rank + ". Take these " + quantity + " Sacred Refinery Packs with my thanks.";
                }
            }
        }
        internal static bool HandleNpc(GameState p, NpcRequest request)
        {
            if (!enabled || p == null || request == null || p.ActiveNpc != DisCityWorld.Saint
                || request.OptionID != 6 && request.OptionID != 60) return false;
            if (!Q.Player(p)) return true;
            lock (p.ItemSyncRoot) try
            {
                if (!Q.Near(p, DisCityWorld.Saint, 1002)) return true;
                var d = new Npcs(p); d.Text(request.OptionID == 60 ? Claim(p) : Board(p));
                d.Option("Claim my rank prize.", 60); d.Option("Farewell.", 255); d.Send();
            }
            catch (Exception ex) { Q.Fail(p, "dis-city-ranking", ex); }
            return true;
        }
    }
}
