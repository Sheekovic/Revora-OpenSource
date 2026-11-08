BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE gameplay_reference.monster_drop_rules (
    source_id uuid NOT NULL,
    monster_type integer NOT NULL,
    ordinal smallint NOT NULL CHECK (ordinal BETWEEN 0 AND 32767),
    item_source_id uuid NOT NULL,
    item_type bigint NOT NULL,
    chance_basis_points integer NOT NULL CHECK (chance_basis_points BETWEEN 0 AND 1000000),
    plus smallint NOT NULL DEFAULT 0 CHECK (plus BETWEEN 0 AND 12),
    sockets smallint NOT NULL DEFAULT 0 CHECK (sockets BETWEEN 0 AND 2),
    bless smallint NOT NULL DEFAULT 0 CHECK (bless BETWEEN 0 AND 7),
    canonical_record_sha256 text NOT NULL CHECK (canonical_record_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (source_id, monster_type, ordinal),
    FOREIGN KEY (source_id, monster_type)
        REFERENCES gameplay_reference.monster_types(source_id, monster_type),
    FOREIGN KEY (item_source_id, item_type)
        REFERENCES gameplay_reference.item_types(source_id, item_type)
);

CREATE TABLE gameplay_reference.equipment_drop_candidates (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    monster_level smallint NOT NULL CHECK (monster_level BETWEEN 0 AND 255),
    item_source_id uuid NOT NULL,
    item_type bigint NOT NULL,
    ordinal smallint NOT NULL CHECK (ordinal BETWEEN 0 AND 32767),
    PRIMARY KEY (source_id, monster_level, ordinal),
    FOREIGN KEY (item_source_id, item_type)
        REFERENCES gameplay_reference.item_types(source_id, item_type)
);

CREATE TABLE game.character_pk_state (
    character_id uuid PRIMARY KEY REFERENCES game.characters(character_id) ON DELETE CASCADE,
    pk_points integer NOT NULL DEFAULT 0 CHECK (pk_points BETWEEN 0 AND 1000000000),
    status text NOT NULL DEFAULT 'normal' CHECK (status IN ('normal', 'flashing', 'red', 'black')),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version > 0)
);

CREATE TABLE game.combat_operation_receipts (
    operation_id uuid PRIMARY KEY,
    attacker_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    target_kind text NOT NULL CHECK (target_kind IN ('character', 'monster')),
    target_legacy_uid bigint NOT NULL CHECK (target_legacy_uid > 0),
    map_id integer NOT NULL CHECK (map_id > 0),
    result text NOT NULL CHECK (result IN ('applied', 'rejected')),
    safe_result_category text NOT NULL CHECK (
        length(safe_result_category) BETWEEN 1 AND 80 AND safe_result_category ~ '^[a-z][a-z0-9-]*$'),
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

GRANT SELECT ON gameplay_reference.monster_drop_rules,
    gameplay_reference.equipment_drop_candidates TO revora_game, revora_backup;
GRANT SELECT, INSERT, UPDATE ON game.character_pk_state TO revora_game;
GRANT SELECT, INSERT ON game.combat_operation_receipts TO revora_game;
GRANT SELECT ON game.character_pk_state, game.combat_operation_receipts TO revora_backup;

COMMIT;
