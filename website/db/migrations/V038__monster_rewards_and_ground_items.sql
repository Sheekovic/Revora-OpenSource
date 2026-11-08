BEGIN;
SET LOCAL ROLE revora_migrator;

ALTER TABLE game.monster_attack_receipts
    ADD COLUMN previous_stamina smallint NULL CHECK (previous_stamina BETWEEN 0 AND 100),
    ADD COLUMN resulting_stamina smallint NULL CHECK (resulting_stamina BETWEEN 0 AND 100),
    ADD COLUMN sitting_interrupted boolean NOT NULL DEFAULT false,
    ADD CONSTRAINT monster_attack_stamina_pair CHECK (
        (previous_stamina IS NULL) = (resulting_stamina IS NULL));

ALTER TABLE game.combat_operation_receipts
    ADD COLUMN previous_target_stamina smallint NULL CHECK (previous_target_stamina BETWEEN 0 AND 100),
    ADD COLUMN resulting_target_stamina smallint NULL CHECK (resulting_target_stamina BETWEEN 0 AND 100),
    ADD COLUMN sitting_interrupted boolean NOT NULL DEFAULT false,
    ADD CONSTRAINT combat_operation_target_stamina_pair CHECK (
        (previous_target_stamina IS NULL) = (resulting_target_stamina IS NULL));

ALTER TABLE gameplay_reference.monster_types
    ADD COLUMN classification text NOT NULL DEFAULT 'normal'
        CHECK (classification IN ('normal','guard','boss','event-special','passive'));

UPDATE gameplay_reference.monster_types SET classification='guard' WHERE behavior IN (4,6);
UPDATE gameplay_reference.monster_types SET classification='boss' WHERE monster_type BETWEEN 3000 AND 4500;
UPDATE gameplay_reference.monster_types SET classification='event-special'
WHERE monster_type=ANY(ARRAY[150,244,247,300,409,500,501,700,701,702,703,704,8423,8424]);
UPDATE gameplay_reference.monster_types SET classification='passive'
WHERE NOT gives_rewards AND classification='normal';

-- Preserve every reviewed generator whose map/DMap dependency is unavailable. These rows are
-- deliberately not FK-bound to gameplay_reference.maps: absence of that reviewed map is the
-- reason they are retained here rather than activated in monster_spawn_regions.
CREATE TABLE gameplay_reference.monster_spawn_dependency_gates (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    spawn_region_id bigint NOT NULL CHECK (spawn_region_id > 0),
    monster_type integer NOT NULL CHECK (monster_type > 0),
    map_id integer NOT NULL CHECK (map_id > 0),
    requested_population integer NOT NULL CHECK (requested_population BETWEEN 1 AND 10000),
    minimum_x integer NOT NULL CHECK (minimum_x BETWEEN 0 AND 65535),
    minimum_y integer NOT NULL CHECK (minimum_y BETWEEN 0 AND 65535),
    maximum_x integer NOT NULL CHECK (maximum_x BETWEEN minimum_x AND 65535),
    maximum_y integer NOT NULL CHECK (maximum_y BETWEEN minimum_y AND 65535),
    dependency_reason text NOT NULL CHECK (dependency_reason IN ('missing-map-dmap','event-or-quest')),
    activation_category text NOT NULL CHECK (activation_category IN ('event-gated','incompatible')),
    canonical_record_sha256 text NOT NULL CHECK (canonical_record_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (source_id,spawn_region_id)
);

CREATE TABLE game.monster_death_resolutions (
    death_id uuid PRIMARY KEY,
    monster_legacy_uid bigint NOT NULL CHECK (monster_legacy_uid BETWEEN 400000000 AND 499999999),
    spawn_generation bigint NOT NULL CHECK (spawn_generation > 0),
    monster_type integer NOT NULL CHECK (monster_type > 0),
    killer_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    map_id integer NOT NULL CHECK (map_id > 0),
    instance_id integer NOT NULL DEFAULT 0 CHECK (instance_id >= 0),
    x integer NOT NULL CHECK (x BETWEEN 0 AND 65535),
    y integer NOT NULL CHECK (y BETWEEN 0 AND 65535),
    experience_awarded bigint NOT NULL CHECK (experience_awarded >= 0),
    skill_experience_awarded bigint NOT NULL CHECK (skill_experience_awarded >= 0),
    resolved_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    UNIQUE (monster_legacy_uid, spawn_generation)
);

CREATE SEQUENCE game.ground_item_uid_seq AS bigint START WITH 300000000 MINVALUE 300000000 MAXVALUE 399999999 NO CYCLE;

CREATE TABLE game.world_ground_drops (
    ground_uid bigint PRIMARY KEY DEFAULT nextval('game.ground_item_uid_seq')
        CHECK (ground_uid BETWEEN 300000000 AND 399999999),
    death_id uuid NOT NULL REFERENCES game.monster_death_resolutions(death_id) ON DELETE RESTRICT,
    item_instance_id uuid NULL UNIQUE REFERENCES game.item_instances(item_instance_id) ON DELETE RESTRICT,
    silver_amount bigint NULL CHECK (silver_amount > 0),
    display_item_type bigint NOT NULL CHECK (display_item_type > 0),
    owner_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    map_id integer NOT NULL CHECK (map_id > 0),
    instance_id integer NOT NULL DEFAULT 0 CHECK (instance_id >= 0),
    x integer NOT NULL CHECK (x BETWEEN 0 AND 65535),
    y integer NOT NULL CHECK (y BETWEEN 0 AND 65535),
    protected_until timestamptz NOT NULL,
    expires_at timestamptz NOT NULL,
    state text NOT NULL DEFAULT 'active' CHECK (state IN ('active','picked-up','expired')),
    picked_up_by uuid NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    picked_up_at timestamptz NULL,
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version > 0),
    CHECK ((item_instance_id IS NOT NULL)::integer + (silver_amount IS NOT NULL)::integer = 1),
    CHECK (expires_at > protected_until),
    CHECK ((state='active' AND picked_up_by IS NULL AND picked_up_at IS NULL) OR
           (state='picked-up' AND picked_up_by IS NOT NULL AND picked_up_at IS NOT NULL) OR
           (state='expired' AND picked_up_by IS NULL AND picked_up_at IS NULL))
);

CREATE UNIQUE INDEX world_ground_drops_active_cell_idx
    ON game.world_ground_drops(map_id,instance_id,x,y) WHERE state='active';
CREATE INDEX world_ground_drops_active_expiry_idx ON game.world_ground_drops(expires_at) WHERE state='active';

CREATE TABLE game.ground_pickup_receipts (
    operation_id uuid PRIMARY KEY,
    ground_uid bigint NOT NULL CHECK (ground_uid BETWEEN 300000000 AND 399999999),
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    request_fingerprint text NOT NULL CHECK (length(request_fingerprint)=64),
    result text NOT NULL CHECK (result IN ('picked-up','replay','protected','full','invalid','expired')),
    resulting_item_uid integer NULL CHECK (resulting_item_uid > 0),
    resulting_silver_balance bigint NULL CHECK (resulting_silver_balance >= 0),
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE FUNCTION game.reject_ground_item_multi_placement() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM game.world_ground_drops d
               WHERE d.item_instance_id=NEW.item_instance_id AND d.state IN ('active','expired')) THEN
        RAISE EXCEPTION 'item instance is active or expired on the ground';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER item_locations_reject_active_ground
BEFORE INSERT OR UPDATE OF item_instance_id ON game.item_locations
FOR EACH ROW EXECUTE FUNCTION game.reject_ground_item_multi_placement();
CREATE TRIGGER warehouse_items_reject_active_ground
BEFORE INSERT OR UPDATE OF item_instance_id ON game.warehouse_items
FOR EACH ROW EXECUTE FUNCTION game.reject_ground_item_multi_placement();

UPDATE gameplay.feature_gates SET required_migration=38 WHERE feature_key IN ('combat.runtime','ninja.native');

GRANT SELECT, INSERT, UPDATE ON game.monster_death_resolutions, game.world_ground_drops,
    game.ground_pickup_receipts TO revora_game;
GRANT USAGE, SELECT ON SEQUENCE game.ground_item_uid_seq TO revora_game;
GRANT SELECT ON game.monster_death_resolutions, game.world_ground_drops,
    game.ground_pickup_receipts TO revora_backup;
GRANT SELECT ON SEQUENCE game.ground_item_uid_seq TO revora_backup;
GRANT SELECT ON gameplay_reference.monster_spawn_dependency_gates TO revora_game, revora_backup;

COMMIT;
