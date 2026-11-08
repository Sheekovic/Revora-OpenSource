BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE SCHEMA IF NOT EXISTS gameplay_reference AUTHORIZATION revora_migrator;

CREATE TABLE gameplay_reference.import_sources (
    source_id uuid PRIMARY KEY,
    archive_sha256 text NOT NULL CHECK (archive_sha256 ~ '^[0-9a-f]{64}$'),
    content_sha256 text NOT NULL CHECK (content_sha256 ~ '^[0-9a-f]{64}$'),
    imported_at timestamptz NOT NULL DEFAULT now(),
    classification text NOT NULL CHECK (classification IN ('reviewed-reference-data')),
    UNIQUE (archive_sha256, content_sha256)
);

CREATE TABLE gameplay_reference.portals (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    legacy_id bigint NOT NULL CHECK (legacy_id > 0),
    source_map integer NOT NULL CHECK (source_map BETWEEN 1 AND 999999999),
    source_x integer NOT NULL CHECK (source_x BETWEEN 0 AND 65535),
    source_y integer NOT NULL CHECK (source_y BETWEEN 0 AND 65535),
    destination_map integer NOT NULL CHECK (destination_map BETWEEN 1 AND 999999999),
    destination_x integer NOT NULL CHECK (destination_x BETWEEN 0 AND 65535),
    destination_y integer NOT NULL CHECK (destination_y BETWEEN 0 AND 65535),
    PRIMARY KEY (source_id, legacy_id)
);

CREATE TABLE gameplay_reference.npcs (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    legacy_uid bigint NOT NULL CHECK (legacy_uid > 0),
    npc_type bigint NOT NULL CHECK (npc_type >= 0),
    flags integer NOT NULL CHECK (flags >= 0),
    face integer NOT NULL CHECK (face >= 0),
    map_id integer NOT NULL CHECK (map_id >= 0),
    x integer NOT NULL CHECK (x BETWEEN 0 AND 65535),
    y integer NOT NULL CHECK (y BETWEEN 0 AND 65535),
    active_month integer NOT NULL CHECK (active_month BETWEEN 0 AND 12),
    active_begin integer NOT NULL CHECK (active_begin >= 0),
    active_end integer NOT NULL CHECK (active_end >= 0),
    destination_map integer NOT NULL CHECK (destination_map >= 0),
    destination_x integer NOT NULL CHECK (destination_x BETWEEN 0 AND 65535),
    destination_y integer NOT NULL CHECK (destination_y BETWEEN 0 AND 65535),
    duration integer NOT NULL CHECK (duration >= 0),
    PRIMARY KEY (source_id, legacy_uid)
);

CREATE TABLE gameplay_reference.shop_entries (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    legacy_id bigint NOT NULL CHECK (legacy_id > 0),
    item_type bigint NOT NULL CHECK (item_type > 0),
    price bigint NOT NULL CHECK (price >= 0),
    currency_type bigint NOT NULL CHECK (currency_type >= 0),
    PRIMARY KEY (source_id, legacy_id)
);

CREATE TABLE gameplay_reference.item_upgrade_rules (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    legacy_unique_id bigint NOT NULL CHECK (legacy_unique_id > 0),
    base_item_type bigint NOT NULL CHECK (base_item_type > 0),
    plus smallint NOT NULL CHECK (plus BETWEEN 0 AND 12),
    health integer NOT NULL CHECK (health >= 0),
    minimum_damage integer NOT NULL CHECK (minimum_damage >= 0),
    maximum_damage integer NOT NULL CHECK (maximum_damage >= minimum_damage),
    defense integer NOT NULL CHECK (defense >= 0),
    magic_damage integer NOT NULL CHECK (magic_damage >= 0),
    magic_defense integer NOT NULL CHECK (magic_defense >= 0),
    accuracy integer NOT NULL CHECK (accuracy >= 0),
    dodge integer NOT NULL CHECK (dodge >= 0),
    PRIMARY KEY (source_id, legacy_unique_id)
);

GRANT USAGE ON SCHEMA gameplay_reference TO revora_game, revora_backup;
GRANT SELECT ON ALL TABLES IN SCHEMA gameplay_reference TO revora_game, revora_backup;

COMMIT;
