using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Local Revora Mr./Ms. edition: orchid titles, tulip wings and combined places2-30.
    // New seasons use the owner-approved Beauty/Wise places9-30 tier.
    internal static class MrMs2016
    {
        internal const string Source = "mrms-2016-revora-v1";
        internal const uint GuideNpc = 380;
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private static readonly object PollGate = new object();
        private static DateTime nextPoll;
        private static volatile bool initialized;
        private sealed class PageView
        {
            internal uint[] Candidates;
            internal string[] Labels;
            internal uint After;
            internal bool More;
        }
        private sealed class Ballot
        {
            internal string Flower;
            internal string Season;
            internal uint[] Candidates = new uint[0];
            internal string[] Labels = new string[0];
            internal uint After;
            internal readonly List<PageView> History = new List<PageView>();
            internal bool More;
            internal List<Tuple<string, byte>> Shown = new List<Tuple<string, byte>>();
        }
        private static Ballot BallotOf(GameState player)
        {
            if (player == null) return null;
            Ballot ballot = player.MrMsBallot as Ballot;
            if (ballot == null)
            {
                ballot = new Ballot();
                player.MrMsBallot = ballot;
            }
            return ballot;
        }

        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_mrms_seasons(
season varchar(7) PRIMARY KEY,closed_at timestamptz NOT NULL);
ALTER TABLE revora_mrms_seasons ADD COLUMN IF NOT EXISTS extended_placements boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS revora_mrms_entrants(
season varchar(7) NOT NULL REFERENCES revora_mrms_seasons(season) DEFERRABLE INITIALLY DEFERRED,
uid bigint NOT NULL CHECK(uid>0),contest varchar(2) NOT NULL CHECK(contest IN ('mr','ms')),
PRIMARY KEY(season,uid));
CREATE TABLE IF NOT EXISTS revora_mrms_votes(
season varchar(7) NOT NULL,voter bigint NOT NULL CHECK(voter>0),
flower varchar(6) NOT NULL CHECK(flower IN ('orchid','tulip')),
contest varchar(2) NOT NULL CHECK(contest IN ('mr','ms')),
candidate bigint NOT NULL CHECK(candidate>0 AND candidate<>voter),
PRIMARY KEY(season,voter,flower,contest));
CREATE TABLE IF NOT EXISTS revora_mrms_awards(
season varchar(7) NOT NULL REFERENCES revora_mrms_seasons(season),
uid bigint NOT NULL CHECK(uid>0),title_key integer NOT NULL CHECK(title_key>0),
award_recorded boolean NOT NULL DEFAULT false,
PRIMARY KEY(season,uid,title_key));
CREATE INDEX IF NOT EXISTS revora_mrms_pending ON revora_mrms_awards(uid) WHERE NOT award_recorded;
CREATE TABLE IF NOT EXISTS revora_mrms_calendar(id integer PRIMARY KEY CHECK(id=1),next_close timestamptz NOT NULL);");
                Q.Execute(c, null, "INSERT INTO revora_mrms_calendar(id,next_close) VALUES(1,@close) ON CONFLICT(id) DO NOTHING",
                    "close", NextMonth(Clock()));
                using (var q = Q.Command(c, null, "SELECT EXISTS(SELECT 1 FROM revora_mrms_calendar WHERE id=1)"))
                    if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Mr./Ms. calendar was not initialized.");
                initialized = true;
            }
        }

        private static DateTime NextMonth(DateTime utc)
        {
            if (utc.Kind != DateTimeKind.Utc) throw new ArgumentException("Mr./Ms. calendar must be UTC.");
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
            return TimeZoneInfo.ConvertTimeToUtc(new DateTime(local.Year, local.Month, 1).AddMonths(1), zone);
        }
        internal static string SeasonOf(DateTime utc)
        {
            if (utc.Kind != DateTimeKind.Utc) throw new ArgumentException("Mr./Ms. season requires UTC.");
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
            return local.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        }
        internal static void Tick()
        {
            if (!initialized || !System.Threading.Monitor.TryEnter(PollGate)) return;
            try
            {
                var now = Clock(); if (now < nextPoll) return; nextPoll = now.AddSeconds(10);
                for (int i = 0; i < 12 && ProcessDue(); i++) { }
            }
            catch (Exception ex) { Console.WriteLine("[mrms] calendar_failure=" + ex.GetType().Name); }
            finally { System.Threading.Monitor.Exit(PollGate); }
        }
        internal static bool ProcessDue()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    DateTime close;
                    using (var q = Q.Command(c, t, "SELECT next_close FROM revora_mrms_calendar WHERE id=1 FOR UPDATE"))
                    using (var r = q.ExecuteReader())
                    {
                        if (!r.Read()) throw new InvalidOperationException("Mr./Ms. calendar missing.");
                        close = r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;
                    }
                    var now = Clock(); if (now.Kind != DateTimeKind.Utc) throw new ArgumentException("Mr./Ms. calendar must be UTC.");
                    if (now < close) { t.Commit(); return false; }
                    var local = TimeZoneInfo.ConvertTimeFromUtc(close, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
                    if (local.Day != 1 || local.TimeOfDay != TimeSpan.Zero || local.Year < 2000)
                        throw new InvalidOperationException("Mr./Ms. calendar boundary is invalid.");
                    string season = local.AddMonths(-1).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
                    Freeze(season, close);
                    if (Q.Execute(c, t, "UPDATE revora_mrms_calendar SET next_close=@next WHERE id=1 AND next_close=@close",
                        "next", NextMonth(close), "close", close) != 1)
                        throw new InvalidOperationException("Mr./Ms. calendar did not advance.");
                    t.Commit(); return true;
                }
            }
        }

        private static int SeasonLock(string season)
        {
            int year, month;
            if (season != null && season.Length == 7 && season[4] == '-' &&
                int.TryParse(season.Substring(0, 4), out year) && int.TryParse(season.Substring(5, 2), out month))
                return 900000 + year * 100 + month;
            return 900000;
        }

        private static void EnsureOpen(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, string season)
        {
            if (string.IsNullOrEmpty(season) || season.Length > 7) throw new ArgumentException("Mr./Ms. season identity is invalid.");
            Q.Execute(c, t, "SELECT pg_advisory_xact_lock(6711,@lock)", "lock", SeasonLock(season));
            using (var q = Q.Command(c, t, "SELECT closed_at='infinity' FROM revora_mrms_seasons WHERE season=@season FOR UPDATE", "season", season))
            {
                object open = q.ExecuteScalar();
                if (object.Equals(open, false)) throw new InvalidOperationException("Mr./Ms. season is already frozen.");
                if (open == null)
                    Q.Execute(c, t, "INSERT INTO revora_mrms_seasons(season,closed_at,extended_placements) VALUES(@season,'infinity',true) ON CONFLICT(season) DO NOTHING", "season", season);
            }
            using (var q = Q.Command(c, t, "SELECT closed_at='infinity' FROM revora_mrms_seasons WHERE season=@season FOR UPDATE", "season", season))
                if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Mr./Ms. season is already frozen.");
        }

        internal static void Register(string season, uint uid, string contest, ushort body)
        {
            if (uid == 0 || (contest != "mr" && contest != "ms")) throw new ArgumentException("Mr./Ms. registration is invalid.");
            bool male = body == 1003 || body == 1004;
            bool female = body == 2001 || body == 2002;
            if (contest == "mr" && !male || contest == "ms" && !female) throw new InvalidOperationException("Mr./Ms. contest does not match the character.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    EnsureOpen(c, t, season);
                    if (Q.Execute(c, t, @"INSERT INTO revora_mrms_entrants(season,uid,contest) VALUES(@season,@uid,@contest)
ON CONFLICT(season,uid) DO NOTHING", "season", season, "uid", (long)uid, "contest", contest) != 1)
                    {
                        using (var q = Q.Command(c, t, "SELECT contest FROM revora_mrms_entrants WHERE season=@season AND uid=@uid", "season", season, "uid", (long)uid))
                            if (!object.Equals(q.ExecuteScalar(), contest)) throw new InvalidOperationException("Mr./Ms. contestant is already entered.");
                    }
                    t.Commit();
                }
            }
        }

        internal static void Vote(string season, uint voter, string flower, string contest, uint candidate)
        {
            if (voter == 0 || voter == candidate || (flower != "orchid" && flower != "tulip") || (contest != "mr" && contest != "ms"))
                throw new ArgumentException("Mr./Ms. vote is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    EnsureOpen(c, t, season);
                    using (var q = Q.Command(c, t, "SELECT contest FROM revora_mrms_entrants WHERE season=@season AND uid=@uid", "season", season, "uid", (long)candidate))
                    {
                        object entered = q.ExecuteScalar();
                        if (!object.Equals(entered, contest)) throw new InvalidOperationException("Mr./Ms. candidate is not entered in that contest.");
                    }
                    if (Q.Execute(c, t, @"INSERT INTO revora_mrms_votes(season,voter,flower,contest,candidate)
VALUES(@season,@voter,@flower,@contest,@candidate) ON CONFLICT(season,voter,flower,contest) DO NOTHING",
                        "season", season, "voter", (long)voter, "flower", flower, "contest", contest, "candidate", (long)candidate) != 1)
                        throw new InvalidOperationException("Mr./Ms. voter already chose in that category.");
                    t.Commit();
                }
            }
        }

        internal static bool Freeze(string season, DateTime closedUtc)
        {
            if (closedUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Mr./Ms. close must be UTC.");
            if (string.IsNullOrEmpty(season) || season.Length > 7) throw new ArgumentException("Mr./Ms. season identity is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    Q.Execute(c, t, "SELECT pg_advisory_xact_lock(6711,@lock)", "lock", SeasonLock(season));
                    using (var q = Q.Command(c, t, "SELECT closed_at='infinity' FROM revora_mrms_seasons WHERE season=@season FOR UPDATE", "season", season))
                    {
                        object open = q.ExecuteScalar();
                        if (object.Equals(open, false))
                        {
                            using (var verify = Q.Command(c, t, "SELECT closed_at=@close FROM revora_mrms_seasons WHERE season=@season", "season", season, "close", closedUtc))
                                if (!object.Equals(verify.ExecuteScalar(), true)) throw new InvalidOperationException("Mr./Ms. season close changed.");
                            t.Commit(); return false;
                        }
                    }
                    EnsureOpen(c, t, season);
                    if (Q.Execute(c, t, "UPDATE revora_mrms_seasons SET closed_at=@close WHERE season=@season AND closed_at='infinity'",
                        "close", closedUtc, "season", season) != 1)
                    {
                        t.Commit(); return false;
                    }
                    using (var q = Q.Command(c, t, @"WITH flora AS (
SELECT contest,flower,candidate AS uid,count(*)::bigint AS votes,
 row_number() OVER (PARTITION BY contest,flower ORDER BY count(*) DESC, candidate) AS placement
FROM revora_mrms_votes WHERE season=@season GROUP BY contest,flower,candidate),
saved AS (
INSERT INTO revora_mrms_awards(season,uid,title_key)
SELECT @season,uid,CASE
 WHEN flower='orchid' AND contest='ms' THEN 20202020
 WHEN flower='orchid' AND contest='mr' THEN 20232023
 WHEN flower='tulip' THEN 60056005 END
FROM flora WHERE placement=1
ON CONFLICT DO NOTHING RETURNING 1)
SELECT (SELECT count(*) FROM flora WHERE placement=1)=(SELECT count(*) FROM saved)", "season", season))
                        if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Mr./Ms. flora awards were not saved.");
                    using (var q = Q.Command(c, t, @"WITH ranked AS (
SELECT contest,candidate AS uid,count(*)::bigint AS votes,
 row_number() OVER (PARTITION BY contest ORDER BY count(*) DESC, candidate) AS placement
FROM revora_mrms_votes WHERE season=@season GROUP BY contest,candidate),
saved AS (
INSERT INTO revora_mrms_awards(season,uid,title_key)
SELECT @season,uid,CASE WHEN placement<=8 THEN CASE WHEN contest='ms' THEN 20212021 ELSE 20242024 END ELSE CASE WHEN contest='ms' THEN 20222022 ELSE 20252025 END END
FROM ranked WHERE placement BETWEEN 2 AND CASE WHEN (SELECT extended_placements FROM revora_mrms_seasons WHERE season=@season) THEN 30 ELSE 8 END
ON CONFLICT DO NOTHING RETURNING 1)
SELECT (SELECT count(*) FROM ranked WHERE placement BETWEEN 2 AND CASE WHEN (SELECT extended_placements FROM revora_mrms_seasons WHERE season=@season) THEN 30 ELSE 8 END)=(SELECT count(*) FROM saved)", "season", season))
                        if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Mr./Ms. placement awards were not saved.");
                    t.Commit(); return true;
                }
            }
        }

        internal static System.Collections.Generic.List<string> RecoverAward(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, uint uid, DateTime utc)
        {
            var notices = new System.Collections.Generic.List<string>();
            var pending = new System.Collections.Generic.List<Tuple<string, int, DateTime>>();
            using (var q = Q.Command(c, t, @"SELECT a.season,a.title_key,s.closed_at
FROM revora_mrms_awards a JOIN revora_mrms_seasons s ON s.season=a.season
WHERE a.uid=@uid AND NOT a.award_recorded AND s.closed_at<=@now AND s.closed_at!='infinity'
ORDER BY s.closed_at,a.title_key LIMIT 8 FOR UPDATE OF a", "uid", (long)uid, "now", utc))
            using (var r = q.ExecuteReader())
                while (r.Read()) pending.Add(Tuple.Create(r.GetString(0), r.GetInt32(1), r.GetFieldValue<DateTimeOffset>(2).UtcDateTime));
            foreach (var award in pending)
            {
                if (!StorageManager.Known(award.Item2 / 10000, award.Item2 % 10000))
                    throw new InvalidOperationException("Mr./Ms. award catalog mapping changed.");
                if (award.Item2 == 20222022 || award.Item2 == 20252025)
                    using(var q=Q.Command(c,t,"SELECT extended_placements FROM revora_mrms_seasons WHERE season=@season","season",award.Item1))
                        if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("This season does not award Beauty or Wise.");
                string key = award.Item1 + ":" + award.Item2.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!ProgressionEvidence.Grant(c, t, uid, Source, key, award.Item2, award.Item3))
                {
                    using (var q = Q.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM revora_event_awards
WHERE uid=@uid AND source=@source AND source_key=@key AND title_key=@title AND earned_at=@earned AND expires_at IS NULL)",
                        "uid", (long)uid, "source", Source, "key", key, "title", award.Item2, "earned", award.Item3))
                        if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Mr./Ms. award was not saved.");
                }
                if (Q.Execute(c, t, @"UPDATE revora_mrms_awards SET award_recorded=true
WHERE season=@season AND uid=@uid AND title_key=@title AND NOT award_recorded",
                    "season", award.Item1, "uid", (long)uid, "title", award.Item2) != 1)
                    throw new InvalidOperationException("Mr./Ms. award receipt was not acknowledged.");
                notices.Add("Your Mr./Ms. Conquer standing earned a wardrobe award. Find it in the wardrobe.");
            }
            return notices;
        }

        internal static bool HandleNpc(GameState player, NpcRequest request)
        {
            if (player == null || request == null || player.ActiveNpc != GuideNpc) return false;
            if (request.OptionID != 0 && (request.OptionID < 220 || request.OptionID > 235)) return false;
            if (request.OptionID == 0) return false;
            try
            {
                string text = Page(player, request.OptionID);
                if (text == null) return true;
                var choices = MenuOptions(player, request.OptionID);
                BallotOf(player).Shown = choices;
                var dialog = new Npcs(player);
                dialog.Text(text);
                foreach (var choice in choices)
                    dialog.Option(choice.Item1, choice.Item2);
                dialog.Send();
            }
            catch (Exception ex) { Console.WriteLine("[mrms] dialogue_failure=" + ex.GetType().Name); }
            return true;
        }

        internal static List<Tuple<string, byte>> MenuOptions(GameState player, byte option)
        {
            var choices = new List<Tuple<string, byte>>();
            Ballot ballot = player != null ? player.MrMsBallot as Ballot : null;
            bool picking = Picking(option) && ballot != null && ballot.Candidates != null && ballot.Candidates.Length > 0;
            if (picking)
            {
                for (int i = 0; i < ballot.Candidates.Length; i++)
                    choices.Add(Tuple.Create(ballot.Labels[i], (byte)(226 + i)));
                if (ballot.More) choices.Add(Tuple.Create("More contestants.", (byte)234));
                if (ballot.History.Count > 0) choices.Add(Tuple.Create("Previous contestants.", (byte)235));
                choices.Add(Tuple.Create("Give an orchid.", (byte)224));
                choices.Add(Tuple.Create("Give a tulip.", (byte)225));
                choices.Add(Tuple.Create("Farewell.", (byte)255));
                return choices;
            }
            choices.Add(Tuple.Create("Standings.", (byte)223));
            choices.Add(Tuple.Create("Enter this month.", (byte)222));
            choices.Add(Tuple.Create("Give an orchid.", (byte)224));
            choices.Add(Tuple.Create("Give a tulip.", (byte)225));
            choices.Add(Tuple.Create("The rules.", (byte)221));
            choices.Add(Tuple.Create("Farewell.", (byte)255));
            return choices;
        }

        private static bool Picking(byte option)
        {
            return option == 224 || option == 225 || option == 234 || option == 235;
        }

        internal static List<Tuple<string, byte>> LastMenu(GameState player)
        {
            Ballot ballot = player == null ? null : player.MrMsBallot as Ballot;
            return ballot == null ? null : ballot.Shown;
        }

        internal static uint[] PinnedCandidates(GameState player)
        {
            Ballot ballot = player == null ? null : player.MrMsBallot as Ballot;
            return ballot == null || ballot.Candidates == null ? new uint[0] : ballot.Candidates;
        }

        private static string ClosedMonth(GameState player)
        {
            player.MrMsBallot = null;
            return "That contest closed at the end of the month.";
        }

        internal static string Page(GameState player, byte option)
        {
            if (option < 220 || option > 235 || !Q.Player(player) || player.ActiveNpc != GuideNpc || player.Map == null ||
                player.Booth != null || player.Trade == null || player.Trade.InTrade) return null;
            Revora.Interfaces.INpc npc;
            if (!player.Map.Npcs.TryGetValue(GuideNpc, out npc) || npc.MapID != player.Entity.MapID ||
                Kernel.GetDistance(player.Entity.X, player.Entity.Y, npc.X, npc.Y) > 6) return null;
            string season = SeasonOf(Clock());
            if (option == 220)
                return "Twin City hosts Mr. and Ms. Conquer this month. Enter, give flowers, or read the standings.";
            if (option == 221)
                return "Each month, heroes in Twin City vote with one orchid and one tulip per contest. Orchid first earns Fairy or Scholar. Tulip first earns Fairy Wings. Places two through eight earn Goddess or Handsome. Equal votes use character number order. New-season places nine through thirty earn Beauty or Wise. Judging is at Pacific midnight when the month ends.";
            if (option == 222)
            {
                string contest = (player.Entity.Body == 1003 || player.Entity.Body == 1004) ? "mr" : "ms";
                Register(season, player.Entity.UID, contest, player.Entity.Body);
                return contest == "ms" ? "You are entered as Ms Conquer this month." : "You are entered as Mr Conquer this month.";
            }
            if (option == 223) return Board(player.Entity.UID, season);
            if (option == 224 || option == 225)
            {
                var ballot = BallotOf(player);
                ballot.Flower = option == 224 ? "orchid" : "tulip";
                ballot.Season = season;
                ballot.History.Clear();
                LoadPage(ballot, season, 0);
                return ballot.Candidates.Length == 0 ? "No one has entered this month." : "Choose a contestant.";
            }
            if (option == 234 || option == 235)
            {
                Ballot ballot = BallotOf(player);
                if (string.IsNullOrEmpty(ballot.Flower)) return "Choose a flower first.";
                if (ballot.Season != season) return ClosedMonth(player);
                if (option == 234 && ballot.More && ballot.Candidates.Length > 0)
                {
                    ballot.History.Add(new PageView
                    {
                        Candidates = ballot.Candidates,
                        Labels = ballot.Labels,
                        After = ballot.After,
                        More = ballot.More
                    });
                    LoadPage(ballot, ballot.Season, ballot.Candidates[ballot.Candidates.Length - 1]);
                }
                else if (option == 235 && ballot.History.Count > 0)
                {
                    PageView previous = ballot.History[ballot.History.Count - 1];
                    ballot.History.RemoveAt(ballot.History.Count - 1);
                    ballot.Candidates = previous.Candidates;
                    ballot.Labels = previous.Labels;
                    ballot.After = previous.After;
                    ballot.More = previous.More;
                }
                return ballot.Candidates.Length == 0 ? "No one has entered this month." : "Choose a contestant.";
            }
            if (option >= 226 && option <= 233)
            {
                Ballot ballot = BallotOf(player);
                if (string.IsNullOrEmpty(ballot.Flower) || ballot.Candidates == null)
                    return "Choose a flower first.";
                if (ballot.Season != season) return ClosedMonth(player);
                int index = option - 226;
                if (index < 0 || index >= ballot.Candidates.Length) return "That contestant is not on this page.";
                uint candidate = ballot.Candidates[index];
                string contest;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open();
                    using (var q = Q.Command(c, null, "SELECT contest FROM revora_mrms_entrants WHERE season=@season AND uid=@uid", "season", ballot.Season, "uid", (long)candidate))
                        contest = q.ExecuteScalar() as string;
                }
                if (contest == null) return "That contestant is not entered.";
                try
                {
                    Vote(ballot.Season, player.Entity.UID, ballot.Flower, contest, candidate);
                }
                catch (InvalidOperationException ex)
                {
                    if (ex.Message != null && ex.Message.IndexOf("already frozen", StringComparison.Ordinal) >= 0)
                        return "That contest has already been judged.";
                    throw;
                }
                return "Your " + ballot.Flower + " is recorded.";
            }
            return null;
        }

        private static void LoadPage(Ballot ballot, string season, uint after)
        {
            var uids = new List<uint>();
            var labels = new List<string>();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var q = Q.Command(c, null, @"SELECT e.uid,e.contest,COALESCE(n.name,'Hero') FROM revora_mrms_entrants e
LEFT JOIN entities n ON n.uid=e.uid WHERE e.season=@season AND e.uid>@after ORDER BY e.uid LIMIT 9",
                    "season", season, "after", (long)after))
                using (var r = q.ExecuteReader())
                {
                    while (uids.Count < 8 && r.Read())
                    {
                        uint uid = checked((uint)r.GetInt64(0));
                        string name = new string(r.GetString(2).Where(ch => !char.IsControl(ch)).Take(16).ToArray());
                        uids.Add(uid);
                        labels.Add(name + " (" + r.GetString(1) + ")");
                    }
                    ballot.More = r.Read();
                }
            }
            ballot.After = after;
            ballot.Candidates = uids.ToArray();
            ballot.Labels = labels.ToArray();
        }
        internal static string Board(uint uid, string season)
        {
            var text = new StringBuilder();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var q = Q.Command(c, null, "SELECT closed_at='infinity' FROM revora_mrms_seasons WHERE season=@season", "season", season))
                {
                    object open = q.ExecuteScalar();
                    if (open == null) return "No one has entered this month.";
                    if (object.Equals(open, true)) text.Append("Voting is open for ").Append(season).Append(". ");
                    else text.Append("Results for ").Append(season).Append(": ");
                }
                using (var q = Q.Command(c, null, @"SELECT a.title_key,COALESCE(n.name,'Hero') FROM revora_mrms_awards a
LEFT JOIN entities n ON n.uid=a.uid WHERE a.season=@season ORDER BY a.title_key,a.uid LIMIT 12", "season", season))
                using (var r = q.ExecuteReader())
                {
                    int count = 0;
                    while (r.Read())
                    {
                        string name = new string(r.GetString(1).Where(ch => !char.IsControl(ch)).Take(16).ToArray());
                        text.Append(r.GetInt32(0)).Append(' ').Append(name).Append(". "); count++;
                    }
                    if (count == 0) text.Append("Standings are not frozen yet. ");
                }
                using (var q = Q.Command(c, null, "SELECT title_key FROM revora_mrms_awards WHERE season=@season AND uid=@uid ORDER BY title_key", "season", season, "uid", (long)uid))
                using (var r = q.ExecuteReader())
                {
                    bool any = false;
                    while (r.Read()) { if (!any) text.Append("Your awards: "); any = true; text.Append(r.GetInt32(0)).Append(' '); }
                    if (!any) text.Append("You have no frozen award this month.");
                }
            }
            return text.ToString();
        }
    }
}
