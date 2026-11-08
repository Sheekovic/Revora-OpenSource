using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Revora.Database;

namespace Revora.Game.Features
{
    // This repository accepts only server-owned transactions. There is deliberately
    // no packet/NPC endpoint for arbitrary completions or event award insertion.
    internal static class ProgressionEvidence
    {
        internal sealed class Award
        {
            internal int Key;
            internal DateTime EarnedUtc;
            internal DateTime? ExpiresUtc;
            internal bool Active(DateTime utc) { return EarnedUtc <= utc && (!ExpiresUtc.HasValue || utc < ExpiresUtc.Value); }
        }
        internal sealed class Snapshot
        {
            internal readonly HashSet<int> Achievements = new HashSet<int>();
            internal readonly List<Award> Awards = new List<Award>();
            internal readonly List<string> Notices = new List<string>();
        }

        internal static void Initialize()
        {
            using (var connection = DataHolder.MySqlConnection) {
                connection.Open();
                using (var transaction = connection.BeginTransaction()) {
                    using (var command = new NpgsqlCommand(@"
CREATE TABLE IF NOT EXISTS revora_achievement_backup_v1 (
    uid bigint PRIMARY KEY CHECK(uid > 0), legacy_words text NOT NULL,
    known_count integer NOT NULL CHECK(known_count BETWEEN 0 AND 337),
    archived_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS revora_achievement_evidence (
    uid bigint NOT NULL CHECK(uid > 0), achievement_id integer NOT NULL,
    source varchar(48) NOT NULL, source_key varchar(96) NOT NULL,
    rule_version integer NOT NULL CHECK(rule_version > 0),
    completed_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY(uid, achievement_id));
CREATE TABLE IF NOT EXISTS revora_event_awards (
    uid bigint NOT NULL CHECK(uid > 0), source varchar(48) NOT NULL,
    source_key varchar(96) NOT NULL, title_key integer NOT NULL,
    earned_at timestamptz NOT NULL, expires_at timestamptz NULL,
    revoked boolean NOT NULL DEFAULT false,
    CHECK(expires_at IS NULL OR expires_at > earned_at),
    PRIMARY KEY(uid, source, source_key));", connection, transaction)) command.ExecuteNonQuery();
                    transaction.Commit();
                }
            }
            // These final receipts are the five sources for the Revora career award.
            MonsterKoCounter.Initialize();
            WildMonsterVictory.Initialize();
            PlayerRevival.Initialize();
            AuraProgress.Initialize();
            PlayerDefeatCredit.Initialize();
            SoulTrialSystem.InitializeRewards();
            CourierLoopSystem.InitializeRewards();
            PathHuntSystem.InitializeRewards();
            FirstRebirth.Initialize();
            SecondRebirth.Initialize();
            AccountActivity.Initialize();
            FashionCompetition.Initialize();
            MrMs2016.Initialize();
            MrMs2017.Initialize();
            OriginRealms.Initialize();
            OriginTournaments.Initialize();
            OriginCtf.Initialize();
            OriginElitePk.Initialize();
            OriginTeamPk.Initialize();
        }

        private static void ValidateIdentity(uint uid, string source, string key)
        {
            if (uid == 0 || !Token(source, 48) || !Token(key, 96)) throw new ArgumentException("Invalid progression evidence identity.");
        }
        private static bool Token(string value, int limit)
        {
            if (string.IsNullOrEmpty(value) || value.Length > limit) return false;
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9'
                    || c == '-' || c == '_' || c == ':' || c == '.')) return false;
            return true;
        }

        internal static bool Complete(NpgsqlConnection connection, NpgsqlTransaction transaction,
            uint uid, int id, string source, string sourceKey, int version)
        {
            ValidateIdentity(uid, source, sourceKey);
            if (transaction == null || transaction.Connection != connection || !AchievementCatalog.Known(id) || version < 1)
                throw new ArgumentException("Invalid achievement completion transaction.");
            using (var command = new NpgsqlCommand(@"INSERT INTO revora_achievement_evidence
(uid,achievement_id,source,source_key,rule_version) VALUES(@uid,@id,@source,@key,@version)
ON CONFLICT(uid,achievement_id) DO NOTHING", connection, transaction)) {
                command.Parameters.AddWithValue("uid", (long)uid); command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("source", source); command.Parameters.AddWithValue("key", sourceKey);
                command.Parameters.AddWithValue("version", version);
                if (command.ExecuteNonQuery() == 1) return true;
            }
            using (var command = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id)", connection, transaction)) {
                command.Parameters.AddWithValue("uid", (long)uid); command.Parameters.AddWithValue("id", id);
                if (!Equals(command.ExecuteScalar(), true))
                    throw new InvalidOperationException("Achievement completion was not saved.");
            }
            return false;
        }

        // Only editions with established identity/lifetime can be stored here.
        // Recording an award still belongs to a future verified event/crafting producer.
        internal static bool AwardRule(string source, int key, out int days)
        {
            days = 0;
            if (source == "planet-revora-v1") { days=30; return key==60096009; }
            if (source == "fashion-revora-v1") { days=30; return key==20522052 || key==20532053 || key==20542054; }
            if (source == "homecoming-revora-v1") { days=30; return key==20282028 || key==20302030 || key==20292029 || key==20312031; }
            if (source == "honored-hero-revora-v1") return key == 20572057;
            if (source == "heart-revora-v1") { days=30; return key==60186018; }
            if (source == "mrms-2017-local-v1") { days=360; return key==20442044 || key==20452045; }
            if (source == "mrms-2017-champion-v1") { days=365; return key==20402040 || key==20412041; }
            if (source == "mrms-2017-wing-1-v1") return key==60126012;
            if (source == "mrms-2017-wing-2-v1") { days=360; return key==60126012; }
            if (source == "mrms-2017-wing-3-v1") { days=180; return key==60126012; }
            if (source == "mrms-2016-revora-v1") return key == 20202020 || key == 20212021 || key == 20232023 || key == 20242024 || key == 20222022 || key == 20252025 || key == 60056005;
            if (source == "holy-grail-craft-v1") return key == 20342034 || key == 60116011;
            if (source == "summer-glow-2017") return key == 60136013 || key == 60146014 || key == 60156015 || key == 60166016;
            if (source == "summer-siren-2017") return key == 60176017;
            // Revora CS-CTF edition: both keys are permanent (expires_at NULL).
            if (source == "cs-ctf-revora-v1") return key == 20030004 || key == 60020021;
            // Revora CS Elite PK edition: fourteen-day wardrobe titles from finalize.
            if (source == "cs-epk-revora-v1") { days = 14; return key == 20352035 || key == 20362036 || key == 20372037 || key == 20382038; }
            if (source == "ctf-local-wardrobe-v1") return key == 20030004 || key == 60020021;
            if (source == "elite-pk-local-wardrobe-v1") { days=14; return key==20352035 || key==20362036 || key==20372037 || key==20382038; }
            if (source == "elite-pk-local-v1") { days = 7; return key == 60010020; }
            if (source == "team-pk-local-wardrobe-v1") { days=14; return key==20592059 || key==20602060 || key==20612061 || key==20622062; }
            if (source == "team-pk-2018") { days = 14; return key == 20592059 || key == 20602060 || key == 20612061 || key == 20622062; }
            if (source == "cs-tpk-revora-v1") { days = 14; return key == 20592059 || key == 20602060 || key == 20612061 || key == 20622062; }
            return false;
        }

        internal static bool Grant(NpgsqlConnection connection, NpgsqlTransaction transaction,
            uint uid, string source, string sourceKey, int titleKey, DateTime earnedUtc)
        {
            ValidateIdentity(uid, source, sourceKey);
            int days;
            if (transaction == null || transaction.Connection != connection || earnedUtc.Kind != DateTimeKind.Utc
                || !AwardRule(source, titleKey, out days)) throw new ArgumentException("Unverified event award definition.");
            using (var command = new NpgsqlCommand(@"INSERT INTO revora_event_awards
(uid,source,source_key,title_key,earned_at,expires_at) VALUES(@uid,@source,@key,@title,@earned,@expiry)
ON CONFLICT(uid,source,source_key) DO NOTHING", connection, transaction)) {
                command.Parameters.AddWithValue("uid", (long)uid); command.Parameters.AddWithValue("source", source);
                command.Parameters.AddWithValue("key", sourceKey); command.Parameters.AddWithValue("title", titleKey);
                command.Parameters.Add("earned", NpgsqlDbType.TimestampTz).Value = earnedUtc;
                command.Parameters.Add("expiry", NpgsqlDbType.TimestampTz).Value = days == 0 ? (object)DBNull.Value : earnedUtc.AddDays(days);
                return command.ExecuteNonQuery() == 1;
            }
        }

        private static void RecoverHomecoming(NpgsqlConnection connection,NpgsqlTransaction transaction,uint uid,DateTime utc)
        {
            // Lock pending receipts before materializing grants; stale or expired history is never rebased.
            var pending=new List<Tuple<string,long,int,DateTime,DateTime,string>>();
            using (var q=new NpgsqlCommand(@"SELECT account_name,auth_number,title_key,returned_at,expires_at,character_snapshot::text
FROM revora_account_returns WHERE character_uid=@uid AND NOT award_recorded AND title_key>0
AND character_snapshot IS NOT NULL AND returned_at<=@now AND expires_at>@now
ORDER BY account_name,auth_number LIMIT 8 FOR UPDATE",connection,transaction))
            {
                q.Parameters.AddWithValue("uid",(long)uid);q.Parameters.Add("now",NpgsqlDbType.TimestampTz).Value=utc;
                using(var r=q.ExecuteReader()) while(r.Read()) pending.Add(Tuple.Create(r.GetString(0),r.GetInt64(1),r.GetInt32(2),
                    r.GetFieldValue<DateTimeOffset>(3).UtcDateTime,r.GetFieldValue<DateTimeOffset>(4).UtcDateTime,r.GetString(5)));
            }
            foreach(var receipt in pending)
            {
                if (receipt.Item5!=receipt.Item4.AddDays(30) || AccountActivity.ReturnTitle(receipt.Item6,receipt.Item4)!=receipt.Item3)
                    throw new InvalidOperationException("Return award does not match its saved snapshot.");
                string key;
                using(var hash=System.Security.Cryptography.SHA256.Create())
                    key="return:"+System.BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(receipt.Item1))).Replace("-","")+":"+receipt.Item2.ToString(System.Globalization.CultureInfo.InvariantCulture);
                const string source="homecoming-revora-v1";
                if (!Grant(connection,transaction,uid,source,key,receipt.Item3,receipt.Item4))
                {
                    using(var q=QuestInventory.Command(connection,transaction,@"SELECT EXISTS(SELECT 1 FROM revora_event_awards
WHERE uid=@uid AND source=@source AND source_key=@key AND title_key=@title AND earned_at=@earned AND expires_at=@expiry)",
                        "uid",(long)uid,"source",source,"key",key,"title",receipt.Item3,"earned",new DateTimeOffset(receipt.Item4),"expiry",new DateTimeOffset(receipt.Item5)))
                        if (!Equals(q.ExecuteScalar(),true)) throw new InvalidOperationException("Return award was not saved.");
                }
                if (QuestInventory.Execute(connection,transaction,@"UPDATE revora_account_returns SET award_recorded=true
WHERE account_name=@name AND auth_number=@number AND character_uid=@uid AND NOT award_recorded",
                    "name",receipt.Item1,"number",receipt.Item2,"uid",(long)uid)!=1)
                    throw new InvalidOperationException("Return award receipt was not consumed.");
            }
        }

        private static void RecoverHonoredHero(NpgsqlConnection connection, NpgsqlTransaction transaction, uint uid, DateTime utc)
        {
            const string source = "honored-hero-revora-v1", key = "career-v1";
            if (!StorageManager.Known(2057,2057) || StorageManager.GetTitlePoints(2057,2057) != 30)
                throw new InvalidOperationException("Honored Hero catalog mapping changed.");
            using (var command = new NpgsqlCommand(@"SELECT
EXISTS(SELECT 1 FROM revora_soul_trial_claims WHERE uid=@uid AND claimed_at<=@now) AND
EXISTS(SELECT 1 FROM revora_courier_claims WHERE uid=@uid AND claimed_at<=@now) AND
EXISTS(SELECT 1 FROM revora_path_hunt_claims WHERE uid=@uid AND claimed_at<=@now) AND
EXISTS(SELECT 1 FROM revora_first_rebirths WHERE uid=@uid AND created_at<=@now) AND
EXISTS(SELECT 1 FROM revora_second_rebirths WHERE uid=@uid AND created_at<=@now)", connection, transaction))
            {
                command.Parameters.AddWithValue("uid",(long)uid);
                command.Parameters.Add("now",NpgsqlDbType.TimestampTz).Value=utc;
                if (!Equals(command.ExecuteScalar(),true)) return;
            }
            if (Grant(connection,transaction,uid,source,key,20572057,utc)) return;
            // A suppressed insert is not an existing grant. Never publish it in memory.
            using (var command = new NpgsqlCommand(@"SELECT EXISTS(SELECT 1 FROM revora_event_awards
WHERE uid=@uid AND source=@source AND source_key=@key AND title_key=20572057 AND expires_at IS NULL)",connection,transaction))
            {
                command.Parameters.AddWithValue("uid",(long)uid);
                command.Parameters.AddWithValue("source",source);command.Parameters.AddWithValue("key",key);
                if (!Equals(command.ExecuteScalar(),true)) throw new InvalidOperationException("Honored Hero award was not saved.");
            }
        }

        // Approved Revora definition: all19 durable final receipts, across lives0-2.
        // Call inside each final reward transaction and on progression recovery.
        internal static void RecordEquipmentQuestAggregate(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            if(t==null || t.Connection!=c || uid==0)throw new ArgumentException("Equipment aggregate requires its completion transaction.");
            using(var q=QuestInventory.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10931)","uid",(long)uid))
                if(Equals(q.ExecuteScalar(),true))return;
            // A partly installed quest catalog cannot qualify. Never initialize other
            // quest schemas merely to inspect them from an unrelated completion.
            using(var q=QuestInventory.Command(c,t,"SELECT bool_and(to_regclass(n) IS NOT NULL) FROM unnest(@tables) n","tables",new string[]{"revora_connector","revora_spun_gold","revora_azure_pill","revora_artisan_feather","revora_mystic_ring","revora_frantic_monkeys","revora_soul_general","revora_mad_man","revora_ghost_city","revora_tomboy","revora_desert_blood","revora_hero_wings","revora_family_feud","revora_desert_treasure","revora_desert_expedition","revora_power_book","revora_defense_talisman","revora_attack_talisman","revora_tempest_wings","revora_power_book_rewards"}))
                if(!Equals(q.ExecuteScalar(),true))return;
            using(var q=QuestInventory.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_connector WHERE uid=@uid AND stage=6 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=3 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=3)
AND EXISTS(SELECT 1 FROM revora_spun_gold WHERE uid=@uid AND stage=8 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=2 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=2)
AND EXISTS(SELECT 1 FROM revora_azure_pill WHERE uid=@uid AND stage=7 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=2 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=2)
AND EXISTS(SELECT 1 FROM revora_artisan_feather WHERE uid=@uid AND stage=6 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=2 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=2)
AND EXISTS(SELECT 1 FROM revora_mystic_ring WHERE uid=@uid AND stage=10 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=2 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=2)
AND EXISTS(SELECT 1 FROM revora_frantic_monkeys WHERE uid=@uid AND stage=10 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=2 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=2)
AND EXISTS(SELECT 1 FROM revora_soul_general WHERE uid=@uid AND stage=12 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=2 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=2)
AND EXISTS(SELECT 1 FROM revora_mad_man WHERE uid=@uid AND stage=9 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=3 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=3)
AND EXISTS(SELECT 1 FROM revora_ghost_city WHERE uid=@uid AND stage=11 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=3 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=3)
AND EXISTS(SELECT 1 FROM revora_tomboy WHERE uid=@uid AND stage=6 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=3 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=3)
AND EXISTS(SELECT 1 FROM revora_desert_blood WHERE uid=@uid AND stage=11 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=3 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=3)
AND EXISTS(SELECT 1 FROM revora_hero_wings WHERE uid=@uid AND stage=12 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=3 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=3)
AND EXISTS(SELECT 1 FROM revora_family_feud WHERE uid=@uid AND stage=10 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=3 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=3)
AND EXISTS(SELECT 1 FROM revora_desert_treasure WHERE uid=@uid AND stage=8 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=3 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=3)
AND EXISTS(SELECT 1 FROM revora_desert_expedition WHERE uid=@uid AND stage=6 AND life BETWEEN 0 AND 2 AND cardinality(rewards)=3 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=3)
AND EXISTS(SELECT 1 FROM revora_power_book p JOIN revora_power_book_rewards r ON r.uid=p.uid AND r.life=p.life WHERE p.uid=@uid AND p.life BETWEEN 0 AND 2 AND p.stage=14 AND r.pack_uid BETWEEN 1 AND 4294967295 AND r.soul_uid BETWEEN 1 AND 4294967295 AND r.pack_uid<>r.soul_uid)
AND EXISTS(SELECT 1 FROM revora_defense_talisman WHERE uid=@uid AND stage=10 AND cardinality(rewards)=2 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=2)
AND EXISTS(SELECT 1 FROM revora_attack_talisman WHERE uid=@uid AND stage=11 AND cardinality(rewards)=2 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=2)
AND EXISTS(SELECT 1 FROM revora_tempest_wings WHERE uid=@uid AND stage=4 AND cardinality(rewards)=2 AND (SELECT count(DISTINCT v) FROM unnest(rewards) v WHERE v BETWEEN 1 AND 4294967295)=2)","uid",(long)uid))
                if(!Equals(q.ExecuteScalar(),true))return;
            ProgressionEvidence.Complete(c,t,uid,10931,"equipment-quests-revora-v1","nineteen-chains-cross-life",1);
        }

        internal static Snapshot Read(uint uid, string legacy, bool archive, IEnumerable<int> observations, DateTime utc)
        {
            return ReadOrigin(uid, legacy, archive, observations, utc, null, Guid.Empty);
        }

        internal static Snapshot ReadOrigin(uint uid, string legacy, bool archive, IEnumerable<int> observations, DateTime utc, string originRealm, Guid originOwner)
        {
            if (uid == 0 || utc.Kind != DateTimeKind.Utc) throw new ArgumentException("Invalid progression snapshot.");
            var snapshot = new Snapshot();
            using (var connection = DataHolder.MySqlConnection) {
                connection.Open();
                using (var transaction = connection.BeginTransaction()) {
                    if (archive) {
                        using (var command = new NpgsqlCommand(@"INSERT INTO revora_achievement_backup_v1(uid,legacy_words,known_count)
VALUES(@uid,@legacy,@count) ON CONFLICT(uid) DO NOTHING", connection, transaction)) {
                            command.Parameters.AddWithValue("uid", (long)uid); command.Parameters.AddWithValue("legacy", legacy ?? "");
                            command.Parameters.AddWithValue("count", AchievementCatalog.LegacyKnownCount(legacy)); command.ExecuteNonQuery();
                        }
                    }
                    foreach (int id in observations) Complete(connection, transaction, uid, id, "observed-state", "predicate-v1", 1);
                    FlowerDelivery.RecordQueen(connection, transaction, uid);
                    MonsterKoCounter.Recover(connection, transaction, uid);
                    PlayerRevival.Recover(connection, transaction, uid);
                    AuraProgress.Recover(connection, transaction, uid);
                    PlayerDefeatCredit.Recover(connection, transaction, uid);
                    GuildWarPurses.Recover(connection, transaction, uid);
                    GuildPoleCredit.Recover(connection, transaction, uid);
                    GuildBeastRuns.Recover(connection, transaction, uid);
                    CollectionAchievements.Record(connection, transaction, uid);
                    EquipmentAchievements.Record(connection, transaction, uid, utc);
                    RecordEquipmentQuestAggregate(connection,transaction,uid);
                    using (var command = new NpgsqlCommand(@"SELECT achievement_id FROM revora_achievement_evidence
WHERE uid=@uid AND achievement_id BETWEEN 10101 AND 11332 AND achievement_id%100 BETWEEN 1 AND 32
ORDER BY achievement_id LIMIT 416", connection, transaction)) {
                        command.Parameters.AddWithValue("uid", (long)uid);
                        using (var reader = command.ExecuteReader())
                            while (reader.Read()) { int id = reader.GetInt32(0); if (AchievementCatalog.Known(id)) snapshot.Achievements.Add(id); }
                    }
                    RecoverHonoredHero(connection,transaction,uid,utc);
                    RecoverHomecoming(connection,transaction,uid,utc);
                    snapshot.Notices.AddRange(FashionCompetition.RecoverAward(connection,transaction,uid,utc));
                    snapshot.Notices.AddRange(MrMs2016.RecoverAward(connection,transaction,uid,utc));
                    snapshot.Notices.AddRange(MrMs2017.RecoverAward(connection,transaction,uid,utc));
                    snapshot.Notices.AddRange(OriginCtf.RecoverAward(connection,transaction,uid,utc,originRealm,originOwner));
                    snapshot.Notices.AddRange(OriginElitePk.RecoverAward(connection,transaction,uid,utc,originRealm,originOwner));
                    snapshot.Notices.AddRange(OriginTeamPk.RecoverAward(connection,transaction,uid,utc,originRealm,originOwner));
                    // A source/title pair has a fixed lifetime. Latest earned_at is
                    // also the latest expiry, and permanent grants remain permanent.
                    // Origin-qualified rows are realm-filtered before DISTINCT ON so a
                    // later equal-uid win on another origin cannot hide this origin's awards.
                    using (var command = new NpgsqlCommand(@"SELECT DISTINCT ON(title_key,source)
title_key,source,earned_at,expires_at,source_key FROM revora_event_awards
WHERE uid=@uid AND NOT revoked AND earned_at<=@now AND (expires_at IS NULL OR expires_at>@now)
AND ((source<>'cs-ctf-revora-v1' AND source<>'cs-epk-revora-v1' AND source<>'cs-tpk-revora-v1') OR (@prefix<>'' AND source_key LIKE @prefix))
ORDER BY title_key,source,earned_at DESC LIMIT 288", connection, transaction)) {
                        command.Parameters.AddWithValue("uid", (long)uid); command.Parameters.Add("now", NpgsqlDbType.TimestampTz).Value = utc;
                        command.Parameters.AddWithValue("prefix", string.IsNullOrEmpty(originRealm) ? "" : originRealm + ":%");
                        using (var reader = command.ExecuteReader()) {
                            while (reader.Read()) {
                                int key = reader.GetInt32(0), days;
                                string source = reader.GetString(1);
                                if (!AwardRule(source, key, out days)) continue;
                                if (source == OriginCtf.Source || source == OriginElitePk.Source || source == OriginTeamPk.Source)
                                {
                                    string sourceKey = reader.GetString(4);
                                    if (string.IsNullOrEmpty(originRealm) || !sourceKey.StartsWith(originRealm + ":", StringComparison.Ordinal))
                                        continue;
                                }
                                DateTime earned = reader.GetFieldValue<DateTimeOffset>(2).UtcDateTime;
                                DateTime? expiry = reader.IsDBNull(3) ? (DateTime?)null : reader.GetFieldValue<DateTimeOffset>(3).UtcDateTime;
                                if (days == 0 ? expiry.HasValue : !expiry.HasValue || expiry.Value != earned.AddDays(days)) continue;
                                snapshot.Awards.Add(new Award { Key = key, EarnedUtc = earned, ExpiresUtc = expiry });
                            }
                        }
                    }
                    transaction.Commit();
                }
            }
            return snapshot;
        }
    }
}
