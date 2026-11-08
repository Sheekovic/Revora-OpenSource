using System;
using System.Collections.Generic;
using System.Globalization;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;
using StorageManager = Revora.StorageManager;

namespace Revora.Game.Features
{
    // Origin-qualified CS Team PK edition: team placements 1/2/3/4-8 of a
    // finalized cs-team-pk-v1 run award King, Dominator, Power and Elite Team
    // to every frozen five-member roster. Pending awards are keyed by
    // (realm, uid); the live session owner authenticates delivery after a
    // process restart.
    internal static class OriginTeamPk
    {
        internal const string Source = "cs-tpk-revora-v1";
        internal const string EventKey = "cs-team-pk-v1";
        internal const int DurationDays = 14;
        internal const int RosterSize = 5;
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;

        internal static int TitleFor(int placement)
        {
            if (placement == 1) return 20592059;
            if (placement == 2) return 20602060;
            if (placement == 3) return 20612061;
            if (placement >= 4 && placement <= 8) return 20622062;
            return 0;
        }

        internal static void Initialize()
        {
            OriginTournaments.Initialize();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var t = c.BeginTransaction())
                {
                    Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_origin_tpk_teams(
team_id uuid PRIMARY KEY,run_id uuid NOT NULL REFERENCES revora_origin_runs(run_id),
realm varchar(32) NOT NULL,captain bigint NOT NULL CHECK(captain>0),
formed_at timestamptz NOT NULL,UNIQUE(run_id,realm,captain));
CREATE TABLE IF NOT EXISTS revora_origin_tpk_roster(
run_id uuid NOT NULL REFERENCES revora_origin_runs(run_id),
team_id uuid NOT NULL REFERENCES revora_origin_tpk_teams(team_id),
realm varchar(32) NOT NULL,uid bigint NOT NULL CHECK(uid>0),
PRIMARY KEY(run_id,realm,uid),UNIQUE(team_id,uid),
FOREIGN KEY(realm,uid) REFERENCES revora_origin_identities(realm,uid));
CREATE TABLE IF NOT EXISTS revora_origin_tpk_awards(
run_id uuid NOT NULL REFERENCES revora_origin_runs(run_id),
realm varchar(32) NOT NULL,uid bigint NOT NULL CHECK(uid>0),
owner uuid NOT NULL,title_key integer NOT NULL CHECK(title_key IN (20592059,20602060,20612061,20622062)),
earned_at timestamptz NOT NULL,award_recorded boolean NOT NULL DEFAULT false,
PRIMARY KEY(run_id,realm,uid,title_key));");
                    Q.Execute(c, t, @"DO $$ BEGIN
IF NOT EXISTS (
SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace
WHERE n.nspname=current_schema() AND t.relname='revora_origin_tpk_awards' AND c.conname='revora_origin_tpk_awards_owner_nonempty') THEN
ALTER TABLE revora_origin_tpk_awards ADD CONSTRAINT revora_origin_tpk_awards_owner_nonempty CHECK (owner<>'00000000-0000-0000-0000-000000000000');
END IF;
END $$;
CREATE INDEX IF NOT EXISTS revora_origin_tpk_pending ON revora_origin_tpk_awards(realm,uid) WHERE NOT award_recorded;");
                    t.Commit();
                }
            }
        }

        internal static Guid FormTeam(Guid run, string realm, uint[] uids, Guid owner)
        {
            if (run == Guid.Empty) throw new ArgumentException("Origin Team PK run is invalid.");
            if (!OriginRealms.Token(realm, 32) || owner == Guid.Empty)
                throw new ArgumentException("Origin Team PK roster is invalid.");
            if (uids == null || uids.Length != RosterSize) throw new ArgumentException("Origin Team PK roster must be five members.");
            var seen = new Dictionary<uint, bool>();
            for (int i = 0; i < uids.Length; i++)
            {
                if (uids[i] == 0 || seen.ContainsKey(uids[i])) throw new ArgumentException("Origin Team PK roster is invalid.");
                seen[uids[i]] = true;
            }
            Guid team = Guid.NewGuid();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    OriginTournaments.LockRun(c, t, run);
                    string eventKey;
                    string state;
                    using (var q = Q.Command(c, t, "SELECT event_key,state FROM revora_origin_runs WHERE run_id=@run FOR UPDATE", "run", run))
                    using (var r = q.ExecuteReader())
                    {
                        if (!r.Read()) throw new InvalidOperationException("Origin Team PK run is unknown.");
                        eventKey = r.GetString(0);
                        state = r.GetString(1);
                    }
                    if (eventKey != EventKey) throw new InvalidOperationException("Origin Team PK event is invalid.");
                    if (state != "open") throw new InvalidOperationException("Origin Team PK roster is frozen.");
                    for (int i = 0; i < uids.Length; i++)
                    {
                        OriginRealms.LockIdentity(c, t, realm, uids[i]);
                        OriginRealms.RequireOwnedSession(c, t, realm, uids[i], owner);
                    }
                    if (Q.Execute(c, t, @"INSERT INTO revora_origin_tpk_teams(team_id,run_id,realm,captain,formed_at)
VALUES(@team,@run,@realm,@captain,@now)",
                        "team", team, "run", run, "realm", realm, "captain", (long)uids[0], "now", Clock()) != 1)
                        throw new InvalidOperationException("Origin Team PK team was not formed.");
                    for (int i = 0; i < uids.Length; i++)
                    {
                        if (Q.Execute(c, t, @"INSERT INTO revora_origin_tpk_roster(run_id,team_id,realm,uid)
VALUES(@run,@team,@realm,@uid)",
                            "run", run, "team", team, "realm", realm, "uid", (long)uids[i]) != 1)
                            throw new InvalidOperationException("Origin Team PK roster was not saved.");
                    }
                    if (Q.Execute(c, t, @"INSERT INTO revora_origin_entrants(run_id,realm,uid,owner,enrolled_at)
VALUES(@run,@realm,@uid,@owner,@now) ON CONFLICT(run_id,realm,uid) DO NOTHING",
                        "run", run, "realm", realm, "uid", (long)uids[0], "owner", owner, "now", Clock()) != 1)
                        throw new InvalidOperationException("Origin Team PK captain was not enrolled.");
                    t.Commit();
                    return team;
                }
            }
        }

        internal static bool Capture(Guid run, Guid coordinator)
        {
            if (run == Guid.Empty) throw new ArgumentException("Origin Team PK run is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    OriginTournaments.LockRun(c, t, run);
                    OriginTournaments.RequireCoordinator(c, t, run, coordinator);
                    string eventKey;
                    string state;
                    DateTime earned;
                    using (var q = Q.Command(c, t, "SELECT event_key,state,finalized_at FROM revora_origin_runs WHERE run_id=@run FOR UPDATE", "run", run))
                    using (var r = q.ExecuteReader())
                    {
                        if (!r.Read()) throw new InvalidOperationException("Origin Team PK run is unknown.");
                        eventKey = r.GetString(0);
                        state = r.GetString(1);
                        if (r.IsDBNull(2)) throw new InvalidOperationException("Origin Team PK run is not finalized.");
                        earned = r.GetFieldValue<DateTimeOffset>(2).UtcDateTime;
                    }
                    if (eventKey != EventKey) throw new InvalidOperationException("Origin Team PK event is invalid.");
                    if (state != "finalized") throw new InvalidOperationException("Origin Team PK run is not finalized.");
                    using (var q = Q.Command(c, t, "SELECT count(DISTINCT realm) FROM revora_origin_entrants WHERE run_id=@run", "run", run))
                        if (Convert.ToInt64(q.ExecuteScalar()) < 2) throw new InvalidOperationException("Origin Team PK needs distinct origins.");
                    using (var q = Q.Command(c, t, @"SELECT EXISTS(
SELECT 1 FROM revora_origin_tpk_teams t
WHERE t.run_id=@run AND (SELECT count(*) FROM revora_origin_tpk_roster r WHERE r.team_id=t.team_id)<>5)
OR EXISTS(SELECT 1 FROM revora_origin_entrants e WHERE e.run_id=@run AND NOT EXISTS(
SELECT 1 FROM revora_origin_tpk_teams t WHERE t.run_id=e.run_id AND t.realm=e.realm AND t.captain=e.uid))
OR EXISTS(SELECT 1 FROM revora_origin_tpk_teams t WHERE t.run_id=@run AND NOT EXISTS(
SELECT 1 FROM revora_origin_entrants e WHERE e.run_id=t.run_id AND e.realm=t.realm AND e.uid=t.captain))
OR EXISTS(SELECT 1 FROM revora_origin_placements p WHERE p.run_id=@run AND NOT EXISTS(
SELECT 1 FROM revora_origin_tpk_teams t WHERE t.run_id=p.run_id AND t.realm=p.realm AND t.captain=p.uid))",
                        "run", run))
                        if (object.Equals(q.ExecuteScalar(), true))
                            throw new InvalidOperationException("Origin Team PK placements must be the stored captains.");
                    var rows = new List<Tuple<string, uint, int, Guid>>();
                    using (var q = Q.Command(c, t, @"SELECT m.realm,m.uid,p.placement,e.owner
FROM revora_origin_placements p
JOIN revora_origin_tpk_teams t ON t.run_id=p.run_id AND t.realm=p.realm AND t.captain=p.uid
JOIN revora_origin_tpk_roster m ON m.team_id=t.team_id
JOIN revora_origin_entrants e ON e.run_id=p.run_id AND e.realm=p.realm AND e.uid=p.uid
WHERE p.run_id=@run AND p.placement BETWEEN 1 AND 8
ORDER BY p.placement,m.uid", "run", run))
                    using (var r = q.ExecuteReader())
                        while (r.Read())
                            rows.Add(Tuple.Create(r.GetString(0), checked((uint)r.GetInt64(1)), r.GetInt32(2), r.GetGuid(3)));
                    int expected = 0, added = 0;
                    foreach (var row in rows)
                    {
                        int title = TitleFor(row.Item3);
                        if (title == 0) continue;
                        if (row.Item4 == Guid.Empty) throw new InvalidOperationException("Origin Team PK award owner is missing.");
                        expected++;
                        added += Q.Execute(c, t, @"INSERT INTO revora_origin_tpk_awards(run_id,realm,uid,owner,title_key,earned_at)
VALUES(@run,@realm,@uid,@owner,@title,@earned) ON CONFLICT DO NOTHING",
                            "run", run, "realm", row.Item1, "uid", (long)row.Item2, "owner", row.Item4, "title", title, "earned", earned);
                    }
                    if (expected == 0) throw new InvalidOperationException("Origin Team PK placements are missing.");
                    t.Commit();
                    return added == expected;
                }
            }
        }

        internal static int VisibleCount(uint uid, string realm)
        {
            if (!OriginRealms.Token(realm, 32) || uid == 0)
                throw new ArgumentException("Origin Team PK delivery is invalid.");
            DateTime utc = Clock();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var q = Q.Command(c, null, @"SELECT count(*) FROM (
SELECT DISTINCT ON(title_key,source) title_key FROM revora_event_awards
WHERE uid=@uid AND source=@source AND NOT revoked AND earned_at<=@now
AND expires_at IS NOT NULL AND expires_at>@now
AND source_key LIKE @prefix
ORDER BY title_key,source,earned_at DESC) s",
                    "uid", (long)uid, "source", Source, "now", utc, "prefix", realm + ":%"))
                    return Convert.ToInt32(q.ExecuteScalar());
            }
        }

        internal static List<string> Deliver(string realm, uint uid, Guid owner)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    var notices = RecoverAward(c, t, uid, Clock(), realm, owner);
                    t.Commit();
                    return notices;
                }
            }
        }

        internal static List<string> RecoverAward(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, uint uid, DateTime utc, string realm, Guid owner)
        {
            var notices = new List<string>();
            if (string.IsNullOrEmpty(realm) || owner == Guid.Empty) return notices;
            if (!OriginRealms.Token(realm, 32) || uid == 0)
                throw new ArgumentException("Origin Team PK delivery is invalid.");
            OriginRealms.LockIdentity(c, t, realm, uid);
            OriginRealms.RequireOwnedSession(c, t, realm, uid, owner);
            var pending = new List<Tuple<Guid, string, int, DateTime>>();
            using (var q = Q.Command(c, t, @"SELECT a.run_id,a.realm,a.title_key,a.earned_at
FROM revora_origin_tpk_awards a JOIN revora_origin_runs r ON r.run_id=a.run_id
WHERE a.realm=@realm AND a.uid=@uid AND NOT a.award_recorded
AND r.state='finalized' AND r.event_key=@event AND a.earned_at<=@now
ORDER BY a.earned_at,a.title_key LIMIT 8 FOR UPDATE OF a",
                "realm", realm, "uid", (long)uid, "event", EventKey, "now", utc))
            using (var r = q.ExecuteReader())
                while (r.Read())
                    pending.Add(Tuple.Create(r.GetGuid(0), r.GetString(1), r.GetInt32(2), r.GetFieldValue<DateTimeOffset>(3).UtcDateTime));
            foreach (var award in pending)
            {
                if (!StorageManager.Known(award.Item3 / 10000, award.Item3 % 10000))
                    throw new InvalidOperationException("CS Team PK award catalog mapping changed.");
                int points = StorageManager.GetTitlePoints((short)(award.Item3 / 10000), (short)(award.Item3 % 10000));
                if (award.Item3 == 20592059 && points != 500) throw new InvalidOperationException("King Team catalog points changed.");
                if (award.Item3 == 20602060 && points != 450) throw new InvalidOperationException("Dominator Team catalog points changed.");
                if (award.Item3 == 20612061 && points != 400) throw new InvalidOperationException("Power Team catalog points changed.");
                if (award.Item3 == 20622062 && points != 300) throw new InvalidOperationException("Elite Team catalog points changed.");
                string key = award.Item2 + ":" + award.Item1.ToString() + ":" + award.Item3.ToString(CultureInfo.InvariantCulture);
                DateTime expiry = award.Item4.AddDays(DurationDays);
                if (!ProgressionEvidence.Grant(c, t, uid, Source, key, award.Item3, award.Item4))
                {
                    using (var q = Q.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM revora_event_awards
WHERE uid=@uid AND source=@source AND source_key=@key AND title_key=@title AND earned_at=@earned AND expires_at=@expiry)",
                        "uid", (long)uid, "source", Source, "key", key, "title", award.Item3, "earned", award.Item4, "expiry", expiry))
                        if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("CS Team PK award was not saved.");
                }
                if (Q.Execute(c, t, @"UPDATE revora_origin_tpk_awards SET award_recorded=true, owner=@owner
WHERE run_id=@run AND realm=@realm AND uid=@uid AND title_key=@title AND NOT award_recorded",
                    "run", award.Item1, "realm", award.Item2, "uid", (long)uid, "title", award.Item3, "owner", owner) != 1)
                    throw new InvalidOperationException("CS Team PK award receipt was not acknowledged.");
                notices.Add("Your cross-server Team PK standing earned a wardrobe award. Find it in the wardrobe.");
            }
            return notices;
        }
    }
}
