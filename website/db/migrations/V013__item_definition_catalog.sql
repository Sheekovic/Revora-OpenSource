BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE gameplay_reference.item_types (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    item_type bigint NOT NULL CHECK (item_type BETWEEN 1 AND 4294967295),
    safe_name text NOT NULL CHECK (
        length(safe_name) BETWEEN 1 AND 128 AND
        safe_name = btrim(safe_name) AND
        safe_name !~ '[[:cntrl:]]'),
    item_class integer NOT NULL CHECK (item_class >= 0),
    equipment_slot smallint NOT NULL DEFAULT 0 CHECK (equipment_slot BETWEEN 0 AND 32),
    required_profession smallint NOT NULL DEFAULT 0 CHECK (required_profession BETWEEN 0 AND 255),
    required_level smallint NOT NULL DEFAULT 0 CHECK (required_level BETWEEN 0 AND 255),
    required_gender smallint NOT NULL DEFAULT 0 CHECK (required_gender BETWEEN 0 AND 2),
    required_weapon_skill integer NOT NULL DEFAULT 0 CHECK (required_weapon_skill BETWEEN 0 AND 65535),
    minimum_attack integer NOT NULL DEFAULT 0 CHECK (minimum_attack >= 0),
    maximum_attack integer NOT NULL DEFAULT 0 CHECK (maximum_attack >= minimum_attack),
    defense integer NOT NULL DEFAULT 0 CHECK (defense >= 0),
    magic_attack integer NOT NULL DEFAULT 0 CHECK (magic_attack >= 0),
    magic_defense integer NOT NULL DEFAULT 0 CHECK (magic_defense >= 0),
    accuracy integer NOT NULL DEFAULT 0 CHECK (accuracy >= 0),
    dodge integer NOT NULL DEFAULT 0 CHECK (dodge >= 0),
    health_bonus integer NOT NULL DEFAULT 0 CHECK (health_bonus >= 0),
    mana_bonus integer NOT NULL DEFAULT 0 CHECK (mana_bonus >= 0),
    attack_range smallint NOT NULL DEFAULT 0 CHECK (attack_range BETWEEN 0 AND 255),
    attack_speed integer NOT NULL DEFAULT 0 CHECK (attack_speed >= 0),
    maximum_durability integer NOT NULL DEFAULT 0 CHECK (maximum_durability BETWEEN 0 AND 65535),
    stack_limit integer NOT NULL DEFAULT 1 CHECK (stack_limit BETWEEN 1 AND 65535),
    silver_price bigint NOT NULL DEFAULT 0 CHECK (silver_price >= 0),
    conquer_point_price bigint NOT NULL DEFAULT 0 CHECK (conquer_point_price >= 0),
    item_flags bigint NOT NULL DEFAULT 0 CHECK (item_flags >= 0),
    safe_description text NOT NULL DEFAULT '' CHECK (
        length(safe_description) <= 2000 AND safe_description !~ '[[:cntrl:]]'),
    canonical_record_sha256 text NOT NULL CHECK (canonical_record_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (source_id, item_type)
);

CREATE TABLE gameplay_reference.item_type_release_entries (
    release_id uuid NOT NULL REFERENCES gameplay_reference.catalog_releases(release_id) ON DELETE CASCADE,
    item_type bigint NOT NULL,
    source_id uuid NOT NULL,
    PRIMARY KEY (release_id, item_type),
    FOREIGN KEY (source_id, item_type)
        REFERENCES gameplay_reference.item_types(source_id, item_type)
);

CREATE INDEX item_types_class_level_idx
    ON gameplay_reference.item_types(item_class, required_level, item_type);

GRANT SELECT ON gameplay_reference.item_types,
    gameplay_reference.item_type_release_entries TO revora_game, revora_backup;

COMMIT;
