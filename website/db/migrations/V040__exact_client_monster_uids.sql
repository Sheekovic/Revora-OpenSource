BEGIN;
SET LOCAL ROLE revora_migrator;

-- V038 initially reserved a server-private 400M range for death receipts.
-- The genuine client cannot select that range, so the durable death identity
-- must use the same exact-client UID domain as the live entity.
ALTER TABLE game.monster_death_resolutions
    DROP CONSTRAINT monster_death_resolutions_monster_legacy_uid_check,
    ADD CONSTRAINT monster_death_resolutions_monster_legacy_uid_check
        CHECK (monster_legacy_uid > 400000 AND monster_legacy_uid < 600000);

-- The approved 5165 executable treats server entities with legacy UIDs strictly
-- between 400000 and 600000 as monsters.  Preserve a durable assignment for
-- every reviewed Owner Source generator member, including currently gated
-- members, so enabling a later dependency cannot renumber existing monsters.
CREATE TABLE gameplay_reference.monster_runtime_uid_assignments (
    source_id uuid NOT NULL,
    spawn_region_id bigint NOT NULL,
    ordinal integer NOT NULL CHECK (ordinal >= 0),
    legacy_uid integer NOT NULL CHECK (legacy_uid > 400000 AND legacy_uid < 600000),
    PRIMARY KEY (source_id, spawn_region_id, ordinal),
    UNIQUE (legacy_uid),
    FOREIGN KEY (source_id, spawn_region_id)
        REFERENCES gameplay_reference.monster_spawn_regions(source_id, spawn_region_id)
        ON DELETE RESTRICT
);

WITH expanded AS (
    SELECT r.source_id,r.spawn_region_id,
           generate_series(0,r.requested_population - 1) AS ordinal
    FROM gameplay_reference.monster_spawn_regions r
), assigned AS (
    SELECT source_id,spawn_region_id,ordinal,
           400000 + row_number() OVER (ORDER BY source_id::text,spawn_region_id,ordinal) AS legacy_uid
    FROM expanded
)
INSERT INTO gameplay_reference.monster_runtime_uid_assignments(
    source_id,spawn_region_id,ordinal,legacy_uid)
SELECT source_id,spawn_region_id,ordinal,legacy_uid
FROM assigned
WHERE legacy_uid < 600000;

DO $$
DECLARE
    expected_count bigint;
    assigned_count bigint;
BEGIN
    SELECT coalesce(sum(requested_population),0)
    INTO expected_count
    FROM gameplay_reference.monster_spawn_regions;

    SELECT count(*) INTO assigned_count
    FROM gameplay_reference.monster_runtime_uid_assignments;

    IF expected_count <> assigned_count THEN
        RAISE EXCEPTION 'Exact-client monster UID space cannot represent the reviewed population (% of % assigned)',
            assigned_count, expected_count;
    END IF;
END $$;

UPDATE gameplay.feature_gates
SET required_migration=40
WHERE feature_key IN ('combat.runtime','monsters','ninja.native');

GRANT SELECT ON gameplay_reference.monster_runtime_uid_assignments TO revora_game,revora_backup;

COMMIT;
