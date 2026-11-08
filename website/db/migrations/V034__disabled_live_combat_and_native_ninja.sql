BEGIN;
SET LOCAL ROLE revora_migrator;

-- Schema support is deliberately installed disabled. Runtime activation still
-- requires reviewed catalog releases, encrypted integration and a canary row.
ALTER TABLE game.characters DROP CONSTRAINT characters_profession_check;
ALTER TABLE game.characters ADD CONSTRAINT characters_profession_check CHECK
    (profession IN (10,11,20,21,40,41,50,51,52,53,54,55,100,132,142));

ALTER TABLE gameplay_reference.base_character_stats
    DROP CONSTRAINT base_character_stats_profession_family_check;
ALTER TABLE gameplay_reference.base_character_stats
    ADD CONSTRAINT base_character_stats_profession_family_check CHECK
        (profession_family IN ('trojan','warrior','archer','taoist','ninja'));

ALTER TABLE game.combat_operation_receipts
    ADD COLUMN request_fingerprint text NOT NULL DEFAULT repeat('0',64)
        CHECK (request_fingerprint ~ '^[0-9a-f]{64}$'),
    ADD COLUMN target_version_before bigint NULL CHECK (target_version_before IS NULL OR target_version_before > 0),
    ADD COLUMN health_before bigint NULL CHECK (health_before IS NULL OR health_before >= 0),
    ADD COLUMN damage bigint NULL CHECK (damage IS NULL OR damage >= 0),
    ADD COLUMN health_after bigint NULL CHECK (health_after IS NULL OR health_after >= 0),
    ADD COLUMN killed boolean NULL,
    ADD COLUMN skill_type integer NULL CHECK (skill_type IS NULL OR skill_type BETWEEN 1 AND 65535),
    ADD COLUMN skill_level smallint NULL CHECK (skill_level IS NULL OR skill_level BETWEEN 0 AND 255),
    ADD COLUMN stamina_before smallint NULL CHECK (stamina_before IS NULL OR stamina_before BETWEEN 0 AND 100),
    ADD COLUMN stamina_after smallint NULL CHECK (stamina_after IS NULL OR stamina_after BETWEEN 0 AND 100),
    ADD COLUMN skill_experience_award bigint NULL CHECK (skill_experience_award IS NULL OR skill_experience_award >= 0);
ALTER TABLE game.combat_operation_receipts ALTER COLUMN request_fingerprint DROP DEFAULT;

CREATE TABLE game.ninja_character_initialization (
    character_id uuid PRIMARY KEY REFERENCES game.characters(character_id) ON DELETE CASCADE,
    account_id uuid NOT NULL,
    legacy_character_uid integer NOT NULL CHECK (legacy_character_uid > 0),
    stats_content_sha256 text NOT NULL CHECK (stats_content_sha256 ~ '^[0-9a-f]{64}$'),
    initialized_profession smallint NOT NULL CHECK (initialized_profession = 50),
    initialized_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    FOREIGN KEY (character_id,account_id) REFERENCES game.characters(character_id,account_id),
    FOREIGN KEY (character_id,legacy_character_uid) REFERENCES game.characters(character_id,legacy_uid)
);

CREATE TABLE game.ninja_promotion_receipts (
    operation_id uuid PRIMARY KEY,
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    account_id uuid NOT NULL,
    legacy_character_uid integer NOT NULL CHECK (legacy_character_uid > 0),
    previous_profession smallint NOT NULL CHECK (previous_profession BETWEEN 50 AND 54),
    resulting_profession smallint NOT NULL CHECK (resulting_profession BETWEEN 51 AND 55),
    character_level smallint NOT NULL CHECK (character_level BETWEEN 1 AND 255),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CHECK (resulting_profession = previous_profession + 1),
    UNIQUE(character_id,resulting_profession),
    FOREIGN KEY (character_id,account_id) REFERENCES game.characters(character_id,account_id),
    FOREIGN KEY (character_id,legacy_character_uid) REFERENCES game.characters(character_id,legacy_uid)
);

CREATE TABLE gameplay_reference.live_monster_spawn_points (
    spawn_point_id uuid PRIMARY KEY,
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    legacy_uid bigint NOT NULL UNIQUE CHECK (legacy_uid BETWEEN 400000000 AND 499999999),
    monster_source_id uuid NOT NULL,
    monster_type integer NOT NULL,
    map_id integer NOT NULL CHECK (map_id > 0),
    x integer NOT NULL CHECK (x BETWEEN 0 AND 65535),
    y integer NOT NULL CHECK (y BETWEEN 0 AND 65535),
    instance_id integer NOT NULL DEFAULT 0 CHECK (instance_id >= 0),
    direction smallint NOT NULL DEFAULT 0 CHECK (direction BETWEEN 0 AND 7),
    enabled boolean NOT NULL DEFAULT false,
    canonical_record_sha256 text NOT NULL CHECK (canonical_record_sha256 ~ '^[0-9a-f]{64}$'),
    FOREIGN KEY (monster_source_id,monster_type)
        REFERENCES gameplay_reference.monster_types(source_id,monster_type)
);

INSERT INTO gameplay_reference.import_sources
    (source_id,archive_sha256,content_sha256,classification)
VALUES
    ('51650034-0000-4e56-8000-000000000001',
     'dbd508b66abf5966e2b326f4a7cc45e20a5350c8f18a9845579f7022b6a7a457',
     'dbc2076b14a98fc5303c916a9b033d347b99a09d3f7e482392c28d3c939e6281',
     'reviewed-reference-data')
ON CONFLICT DO NOTHING;

-- Exact fifth 120-level block decoded as four little-endian UInt16 fields from
-- the hash-pinned owner Stats.dat. Row order is the authoritative level.
INSERT INTO gameplay_reference.base_character_stats
    (source_id,profession_family,level,strength,agility,vitality,spirit)
VALUES
    ('51650034-0000-4e56-8000-000000000001','ninja',1,1,8,1,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',2,1,11,1,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',3,1,13,1,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',4,2,16,2,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',5,2,18,2,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',6,2,21,2,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',7,3,23,3,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',8,4,26,3,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',9,4,28,3,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',10,5,31,4,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',11,5,33,4,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',12,5,36,4,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',13,6,38,5,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',14,6,41,6,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',15,7,43,6,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',16,8,46,7,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',17,8,48,7,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',18,8,51,7,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',19,9,53,8,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',20,9,56,8,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',21,9,58,8,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',22,11,61,9,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',23,11,63,9,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',24,11,66,9,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',25,12,68,10,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',26,12,71,10,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',27,12,73,11,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',28,13,76,12,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',29,14,78,12,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',30,14,81,12,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',31,15,83,13,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',32,15,86,13,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',33,15,88,13,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',34,16,91,14,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',35,16,93,14,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',36,17,96,14,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',37,18,98,15,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',38,18,101,15,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',39,18,103,15,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',40,19,106,17,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',41,19,108,17,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',42,19,111,17,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',43,21,113,18,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',44,21,116,18,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',45,21,118,18,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',46,22,121,19,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',47,22,123,19,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',48,22,126,19,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',49,23,128,20,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',50,24,131,20,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',51,24,133,20,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',52,25,136,21,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',53,25,138,22,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',54,25,141,22,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',55,26,143,23,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',56,26,146,23,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',57,27,148,23,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',58,28,151,24,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',59,28,153,24,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',60,28,156,24,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',61,29,158,25,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',62,29,161,25,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',63,29,163,25,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',64,31,166,26,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',65,31,168,26,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',66,31,171,27,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',67,32,173,28,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',68,32,176,28,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',69,32,178,28,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',70,33,181,29,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',71,34,183,29,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',72,34,186,29,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',73,35,188,30,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',74,35,191,30,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',75,35,193,30,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',76,36,196,31,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',77,36,198,31,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',78,37,201,31,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',79,38,203,33,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',80,38,206,33,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',81,38,208,33,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',82,39,211,34,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',83,39,213,34,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',84,39,216,34,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',85,41,218,35,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',86,41,221,35,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',87,41,223,35,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',88,42,226,36,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',89,42,228,36,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',90,42,231,36,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',91,43,233,37,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',92,44,236,38,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',93,44,238,38,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',94,45,241,39,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',95,45,243,39,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',96,45,246,39,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',97,46,248,40,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',98,46,251,40,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',99,47,253,40,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',100,48,256,41,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',101,48,258,41,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',102,48,261,41,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',103,49,263,42,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',104,49,266,42,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',105,49,268,43,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',106,51,271,44,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',107,51,273,44,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',108,51,276,44,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',109,52,278,45,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',110,52,281,45,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',111,52,283,45,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',112,53,286,46,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',113,54,288,46,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',114,54,291,46,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',115,55,293,47,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',116,55,296,47,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',117,55,298,47,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',118,56,301,49,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',119,56,303,49,0),
    ('51650034-0000-4e56-8000-000000000001','ninja',120,57,306,49,0)
ON CONFLICT DO NOTHING;

INSERT INTO gameplay.feature_gates(feature_key,gate_state,required_migration,required_release_id,changed_by)
VALUES
    ('combat.runtime','disabled',34,NULL,'migration-v034'),
    ('ninja.native','disabled',34,NULL,'migration-v034')
ON CONFLICT (feature_key) DO NOTHING;

INSERT INTO gameplay.feature_gate_dependencies(feature_key,depends_on_feature_key) VALUES
    ('combat.runtime','combat.pk'),
    ('combat.runtime','monsters'),
    ('ninja.native','combat.runtime'),
    ('ninja.native','skills'),
    ('ninja.native','progression.rebirth')
ON CONFLICT DO NOTHING;

INSERT INTO gameplay.feature_rollouts(feature_key,rollout_scope,changed_by)
VALUES
    ('combat.runtime','disabled','migration-v034'),
    ('ninja.native','disabled','migration-v034')
ON CONFLICT (feature_key) DO NOTHING;

GRANT SELECT,INSERT ON game.ninja_character_initialization TO revora_game;
GRANT SELECT,INSERT ON game.ninja_promotion_receipts TO revora_game;
GRANT SELECT ON game.ninja_character_initialization,game.ninja_promotion_receipts TO revora_backup;
GRANT SELECT ON gameplay_reference.live_monster_spawn_points TO revora_game,revora_backup;

COMMIT;
