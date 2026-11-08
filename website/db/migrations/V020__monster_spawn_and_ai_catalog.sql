BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE gameplay_reference.monster_types (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    monster_type integer NOT NULL CHECK (monster_type > 0),
    safe_name text NOT NULL CHECK (length(safe_name) BETWEEN 1 AND 128 AND safe_name !~ '[[:cntrl:]]'),
    behavior smallint NOT NULL CHECK (behavior BETWEEN 0 AND 255),
    mesh integer NOT NULL CHECK (mesh BETWEEN 0 AND 65535),
    level smallint NOT NULL CHECK (level BETWEEN 0 AND 255),
    maximum_health bigint NOT NULL CHECK (maximum_health > 0),
    defense integer NOT NULL CHECK (defense >= 0),
    magic_defense integer NOT NULL CHECK (magic_defense >= 0),
    magic_attack integer NOT NULL CHECK (magic_attack >= 0),
    minimum_attack integer NOT NULL CHECK (minimum_attack >= 0),
    maximum_attack integer NOT NULL CHECK (maximum_attack >= minimum_attack),
    dodge smallint NOT NULL CHECK (dodge BETWEEN 0 AND 255),
    attack_type smallint NOT NULL CHECK (attack_type BETWEEN 0 AND 255),
    attack_distance smallint NOT NULL CHECK (attack_distance BETWEEN 0 AND 255),
    move_delay_ms integer NOT NULL CHECK (move_delay_ms >= 0),
    respawn_delay_ms bigint NOT NULL CHECK (respawn_delay_ms >= 0),
    canonical_record_sha256 text NOT NULL CHECK (canonical_record_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (source_id, monster_type)
);

CREATE TABLE gameplay_reference.monster_spawn_regions (
    source_id uuid NOT NULL,
    spawn_region_id bigint NOT NULL CHECK (spawn_region_id > 0),
    monster_type integer NOT NULL,
    map_source_id uuid NOT NULL,
    map_id integer NOT NULL,
    requested_population integer NOT NULL CHECK (requested_population BETWEEN 1 AND 10000),
    minimum_x integer NOT NULL CHECK (minimum_x BETWEEN 0 AND 65535),
    minimum_y integer NOT NULL CHECK (minimum_y BETWEEN 0 AND 65535),
    maximum_x integer NOT NULL CHECK (maximum_x BETWEEN minimum_x AND 65535),
    maximum_y integer NOT NULL CHECK (maximum_y BETWEEN minimum_y AND 65535),
    enabled boolean NOT NULL DEFAULT false,
    canonical_record_sha256 text NOT NULL CHECK (canonical_record_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (source_id, spawn_region_id),
    FOREIGN KEY (source_id, monster_type)
        REFERENCES gameplay_reference.monster_types(source_id, monster_type),
    FOREIGN KEY (map_source_id, map_id)
        REFERENCES gameplay_reference.maps(source_id, legacy_map_id)
);

CREATE TABLE gameplay_reference.monster_release_entries (
    release_id uuid NOT NULL REFERENCES gameplay_reference.catalog_releases(release_id) ON DELETE CASCADE,
    source_id uuid NOT NULL,
    monster_type integer NOT NULL,
    PRIMARY KEY (release_id, monster_type),
    FOREIGN KEY (source_id, monster_type)
        REFERENCES gameplay_reference.monster_types(source_id, monster_type)
);

CREATE TABLE gameplay_reference.spawn_release_entries (
    release_id uuid NOT NULL REFERENCES gameplay_reference.catalog_releases(release_id) ON DELETE CASCADE,
    source_id uuid NOT NULL,
    spawn_region_id bigint NOT NULL,
    PRIMARY KEY (release_id, spawn_region_id),
    FOREIGN KEY (source_id, spawn_region_id)
        REFERENCES gameplay_reference.monster_spawn_regions(source_id, spawn_region_id)
);

GRANT SELECT ON gameplay_reference.monster_types,
    gameplay_reference.monster_spawn_regions,
    gameplay_reference.monster_release_entries,
    gameplay_reference.spawn_release_entries TO revora_game, revora_backup;

COMMIT;
