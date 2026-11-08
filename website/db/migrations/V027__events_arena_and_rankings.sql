BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE gameplay_reference.event_definitions (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    event_id bigint NOT NULL CHECK (event_id > 0),
    safe_name text NOT NULL CHECK (length(safe_name) BETWEEN 1 AND 160 AND safe_name !~ '[[:cntrl:]]'),
    event_kind text NOT NULL CHECK (event_kind IN ('tournament', 'arena', 'map-event')),
    map_source_id uuid NOT NULL,
    map_id integer NOT NULL,
    minimum_level smallint NOT NULL DEFAULT 1 CHECK (minimum_level BETWEEN 1 AND 255),
    maximum_participants integer NOT NULL CHECK (maximum_participants BETWEEN 2 AND 10000),
    enabled boolean NOT NULL DEFAULT false,
    canonical_record_sha256 text NOT NULL CHECK (canonical_record_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (source_id, event_id),
    FOREIGN KEY (map_source_id, map_id)
        REFERENCES gameplay_reference.maps(source_id, legacy_map_id)
);

CREATE TABLE game.event_instances (
    event_instance_id uuid PRIMARY KEY,
    event_source_id uuid NOT NULL,
    event_id bigint NOT NULL,
    state text NOT NULL CHECK (state IN ('registration', 'running', 'completed', 'cancelled')),
    starts_at timestamptz NOT NULL,
    ended_at timestamptz NULL,
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version > 0),
    FOREIGN KEY (event_source_id, event_id)
        REFERENCES gameplay_reference.event_definitions(source_id, event_id),
    CHECK ((state IN ('completed', 'cancelled')) = (ended_at IS NOT NULL))
);

CREATE TABLE game.event_participants (
    event_instance_id uuid NOT NULL REFERENCES game.event_instances(event_instance_id) ON DELETE CASCADE,
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    score bigint NOT NULL DEFAULT 0,
    placement integer NULL CHECK (placement > 0),
    joined_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY (event_instance_id, character_id)
);

CREATE TABLE game.ranking_snapshots (
    snapshot_id uuid PRIMARY KEY,
    realm_id uuid NOT NULL REFERENCES game.realms(realm_id) ON DELETE CASCADE,
    ranking_kind text NOT NULL CHECK (ranking_kind IN ('level', 'arena', 'guild', 'pk')),
    generated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    expires_at timestamptz NOT NULL,
    CHECK (expires_at > generated_at)
);

CREATE TABLE game.ranking_snapshot_entries (
    snapshot_id uuid NOT NULL REFERENCES game.ranking_snapshots(snapshot_id) ON DELETE CASCADE,
    position integer NOT NULL CHECK (position > 0),
    character_id uuid NULL REFERENCES game.characters(character_id) ON DELETE SET NULL,
    guild_id uuid NULL REFERENCES game.guilds(guild_id) ON DELETE SET NULL,
    score bigint NOT NULL,
    PRIMARY KEY (snapshot_id, position),
    CHECK ((character_id IS NULL) <> (guild_id IS NULL))
);

GRANT SELECT ON gameplay_reference.event_definitions TO revora_game, revora_backup;
GRANT SELECT, INSERT, UPDATE, DELETE ON game.event_instances,
    game.event_participants, game.ranking_snapshots, game.ranking_snapshot_entries TO revora_game;
GRANT SELECT ON game.event_instances, game.event_participants,
    game.ranking_snapshots, game.ranking_snapshot_entries TO revora_backup;

COMMIT;
