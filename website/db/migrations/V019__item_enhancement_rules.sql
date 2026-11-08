BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE gameplay_reference.item_enhancement_rules (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    item_family bigint NOT NULL CHECK (item_family BETWEEN 1 AND 4294967295),
    plus smallint NOT NULL CHECK (plus BETWEEN 0 AND 12),
    health_bonus integer NOT NULL DEFAULT 0 CHECK (health_bonus >= 0),
    minimum_attack_bonus integer NOT NULL DEFAULT 0 CHECK (minimum_attack_bonus >= 0),
    maximum_attack_bonus integer NOT NULL DEFAULT 0 CHECK (maximum_attack_bonus >= minimum_attack_bonus),
    defense_bonus integer NOT NULL DEFAULT 0 CHECK (defense_bonus >= 0),
    magic_attack_bonus integer NOT NULL DEFAULT 0 CHECK (magic_attack_bonus >= 0),
    magic_defense_bonus integer NOT NULL DEFAULT 0 CHECK (magic_defense_bonus >= 0),
    accuracy_bonus integer NOT NULL DEFAULT 0 CHECK (accuracy_bonus >= 0),
    dodge_bonus integer NOT NULL DEFAULT 0 CHECK (dodge_bonus >= 0),
    canonical_record_sha256 text NOT NULL CHECK (canonical_record_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (source_id, item_family, plus)
);

CREATE TABLE gameplay_reference.item_enhancement_release_entries (
    release_id uuid NOT NULL REFERENCES gameplay_reference.catalog_releases(release_id) ON DELETE CASCADE,
    source_id uuid NOT NULL,
    item_family bigint NOT NULL,
    plus smallint NOT NULL,
    PRIMARY KEY (release_id, item_family, plus),
    FOREIGN KEY (source_id, item_family, plus)
        REFERENCES gameplay_reference.item_enhancement_rules(source_id, item_family, plus)
);

CREATE TABLE game.item_enhancement_operations (
    operation_id uuid PRIMARY KEY,
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    item_instance_id uuid NOT NULL,
    operation_kind text NOT NULL CHECK (operation_kind IN
        ('repair', 'compose', 'quality-upgrade', 'level-upgrade', 'enchant', 'socket', 'gem-insert', 'gem-remove')),
    expected_item_version bigint NOT NULL CHECK (expected_item_version > 0),
    resulting_item_version bigint NULL CHECK (resulting_item_version > expected_item_version),
    result text NOT NULL CHECK (result IN ('committed', 'rejected')),
    safe_result_category text NOT NULL CHECK (
        length(safe_result_category) BETWEEN 1 AND 80 AND
        safe_result_category ~ '^[a-z][a-z0-9-]*$'),
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    FOREIGN KEY (item_instance_id, character_id)
        REFERENCES game.item_instances(item_instance_id, owner_character_id)
);

CREATE INDEX item_enhancement_operations_character_time_idx
    ON game.item_enhancement_operations(character_id, occurred_at DESC);

GRANT SELECT ON gameplay_reference.item_enhancement_rules,
    gameplay_reference.item_enhancement_release_entries TO revora_game, revora_backup;
GRANT SELECT, INSERT ON game.item_enhancement_operations TO revora_game;
GRANT SELECT ON game.item_enhancement_operations TO revora_backup;

COMMIT;
