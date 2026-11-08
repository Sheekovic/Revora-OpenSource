BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE gameplay_reference.maps (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    legacy_map_id integer NOT NULL CHECK (legacy_map_id > 0),
    safe_name text NOT NULL CHECK (
        length(safe_name) BETWEEN 1 AND 128 AND
        safe_name = btrim(safe_name) AND
        safe_name !~ '[[:cntrl:]]'),
    map_flags bigint NOT NULL DEFAULT 0 CHECK (map_flags >= 0),
    reborn_map_id integer NULL CHECK (reborn_map_id > 0),
    reborn_x integer NULL CHECK (reborn_x BETWEEN 0 AND 65535),
    reborn_y integer NULL CHECK (reborn_y BETWEEN 0 AND 65535),
    canonical_record_sha256 text NOT NULL CHECK (canonical_record_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (source_id, legacy_map_id),
    CHECK ((reborn_map_id IS NULL AND reborn_x IS NULL AND reborn_y IS NULL) OR
           (reborn_map_id IS NOT NULL AND reborn_x IS NOT NULL AND reborn_y IS NOT NULL))
);

CREATE TABLE gameplay_reference.map_documents (
    source_id uuid NOT NULL,
    legacy_map_id integer NOT NULL,
    document_id integer NOT NULL CHECK (document_id > 0),
    dmap_artifact_sha256 text NOT NULL REFERENCES gameplay_reference.source_artifacts(artifact_sha256),
    dmap_byte_length bigint NOT NULL CHECK (dmap_byte_length > 0),
    width integer NOT NULL CHECK (width BETWEEN 1 AND 4096),
    height integer NOT NULL CHECK (height BETWEEN 1 AND 4096),
    format_version text NOT NULL CHECK (format_version IN ('1003', '1004', 'DMAP100', 'DMAP101')),
    PRIMARY KEY (source_id, legacy_map_id),
    UNIQUE (source_id, document_id),
    FOREIGN KEY (source_id, legacy_map_id)
        REFERENCES gameplay_reference.maps(source_id, legacy_map_id),
    CHECK ((width::bigint * height::bigint) <= 16777216)
);

CREATE TABLE gameplay_reference.map_release_entries (
    release_id uuid NOT NULL REFERENCES gameplay_reference.catalog_releases(release_id) ON DELETE CASCADE,
    legacy_map_id integer NOT NULL,
    source_id uuid NOT NULL,
    PRIMARY KEY (release_id, legacy_map_id),
    FOREIGN KEY (source_id, legacy_map_id)
        REFERENCES gameplay_reference.maps(source_id, legacy_map_id)
);

CREATE INDEX maps_reborn_destination_idx
    ON gameplay_reference.maps(source_id, reborn_map_id)
    WHERE reborn_map_id IS NOT NULL;

GRANT SELECT ON gameplay_reference.maps,
    gameplay_reference.map_documents,
    gameplay_reference.map_release_entries TO revora_game, revora_backup;

COMMIT;
