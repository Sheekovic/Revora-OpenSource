BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE SCHEMA IF NOT EXISTS gameplay AUTHORIZATION revora_migrator;

CREATE TABLE gameplay_reference.source_artifacts (
    artifact_sha256 text PRIMARY KEY CHECK (artifact_sha256 ~ '^[0-9a-f]{64}$'),
    safe_filename text NOT NULL CHECK (
        length(safe_filename) BETWEEN 1 AND 160 AND
        safe_filename = btrim(safe_filename) AND
        safe_filename !~ '[\\/]' AND
        safe_filename !~ '[[:cntrl:]]'),
    byte_length bigint NOT NULL CHECK (byte_length >= 0),
    media_type text NOT NULL CHECK (length(media_type) BETWEEN 1 AND 80),
    format_classification text NOT NULL CHECK (format_classification IN (
        'owner-archive', 'owner-database', 'map-index', 'dmap', 'item-catalog',
        'item-upgrade-catalog', 'shop-catalog', 'monster-catalog', 'spawn-catalog',
        'drop-catalog', 'skill-catalog', 'experience-catalog', 'portal-catalog',
        'npc-catalog', 'other-reviewed-reference')),
    parent_artifact_sha256 text NULL REFERENCES gameplay_reference.source_artifacts(artifact_sha256),
    parser_name text NOT NULL CHECK (length(parser_name) BETWEEN 1 AND 100),
    parser_version integer NOT NULL CHECK (parser_version > 0),
    review_state text NOT NULL CHECK (review_state IN ('quarantined', 'reviewed', 'rejected')),
    recorded_at timestamptz NOT NULL DEFAULT now(),
    CHECK (parent_artifact_sha256 IS NULL OR parent_artifact_sha256 <> artifact_sha256)
);

CREATE TABLE gameplay_reference.import_batches (
    batch_id uuid PRIMARY KEY,
    source_artifact_sha256 text NOT NULL REFERENCES gameplay_reference.source_artifacts(artifact_sha256),
    canonical_manifest_sha256 text NOT NULL CHECK (canonical_manifest_sha256 ~ '^[0-9a-f]{64}$'),
    parser_name text NOT NULL CHECK (length(parser_name) BETWEEN 1 AND 100),
    parser_version integer NOT NULL CHECK (parser_version > 0),
    validation_state text NOT NULL CHECK (validation_state IN ('staged', 'validated', 'rejected')),
    started_at timestamptz NOT NULL DEFAULT now(),
    completed_at timestamptz NULL,
    UNIQUE (source_artifact_sha256, canonical_manifest_sha256, parser_name, parser_version),
    CHECK ((validation_state = 'staged' AND completed_at IS NULL) OR
           (validation_state <> 'staged' AND completed_at IS NOT NULL))
);

CREATE TABLE gameplay_reference.catalog_releases (
    release_id uuid PRIMARY KEY,
    realm_id uuid NOT NULL REFERENCES game.realms(realm_id),
    manifest_sha256 text NOT NULL CHECK (manifest_sha256 ~ '^[0-9a-f]{64}$'),
    release_state text NOT NULL CHECK (release_state IN ('staged', 'validated', 'active', 'retired')),
    created_at timestamptz NOT NULL DEFAULT now(),
    activated_at timestamptz NULL,
    UNIQUE (realm_id, manifest_sha256),
    CHECK ((release_state = 'active' AND activated_at IS NOT NULL) OR release_state <> 'active')
);

CREATE UNIQUE INDEX catalog_releases_one_active_per_realm_uq
    ON gameplay_reference.catalog_releases(realm_id)
    WHERE release_state = 'active';

CREATE TABLE gameplay_reference.catalog_release_categories (
    release_id uuid NOT NULL REFERENCES gameplay_reference.catalog_releases(release_id) ON DELETE CASCADE,
    category text NOT NULL CHECK (category ~ '^[a-z][a-z0-9.-]{1,63}$'),
    batch_id uuid NOT NULL REFERENCES gameplay_reference.import_batches(batch_id),
    expected_row_count bigint NOT NULL CHECK (expected_row_count >= 0),
    actual_row_count bigint NOT NULL CHECK (actual_row_count >= 0),
    content_sha256 text NOT NULL CHECK (content_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (release_id, category),
    CHECK (expected_row_count = actual_row_count)
);

CREATE TABLE gameplay.feature_gates (
    feature_key text PRIMARY KEY CHECK (feature_key ~ '^[a-z][a-z0-9.]{2,63}$'),
    gate_state text NOT NULL DEFAULT 'disabled' CHECK (gate_state IN ('disabled', 'validated', 'enabled')),
    required_migration integer NOT NULL CHECK (required_migration >= 11),
    required_release_id uuid NULL REFERENCES gameplay_reference.catalog_releases(release_id),
    enabled_version bigint NOT NULL DEFAULT 0 CHECK (enabled_version >= 0),
    changed_at timestamptz NOT NULL DEFAULT now(),
    changed_by text NOT NULL DEFAULT 'migration' CHECK (length(changed_by) BETWEEN 1 AND 100),
    CHECK (gate_state <> 'enabled' OR required_release_id IS NOT NULL)
);

CREATE TABLE gameplay.feature_gate_dependencies (
    feature_key text NOT NULL REFERENCES gameplay.feature_gates(feature_key) ON DELETE CASCADE,
    depends_on_feature_key text NOT NULL REFERENCES gameplay.feature_gates(feature_key),
    PRIMARY KEY (feature_key, depends_on_feature_key),
    CHECK (feature_key <> depends_on_feature_key)
);

INSERT INTO gameplay.feature_gates (feature_key, required_migration) VALUES
    ('world.catalog.complete', 12),
    ('items.definitions', 13),
    ('inventory', 14),
    ('currency.ledger', 15),
    ('equipment.stats', 16),
    ('shops', 17),
    ('warehouse', 18),
    ('items.enhancement', 19),
    ('monsters', 20),
    ('combat.pk', 21),
    ('drops', 21),
    ('skills', 22),
    ('progression.rebirth', 22),
    ('social.basic', 23),
    ('trade.personalshops', 24),
    ('guild.marriage', 25),
    ('npc.quests', 26),
    ('events.arena.rankings', 27),
    ('housing.mining.weather', 28),
    ('chat.gm', 29)
ON CONFLICT (feature_key) DO NOTHING;

INSERT INTO gameplay.feature_gate_dependencies (feature_key, depends_on_feature_key) VALUES
    ('items.definitions', 'world.catalog.complete'),
    ('inventory', 'items.definitions'),
    ('equipment.stats', 'inventory'),
    ('shops', 'world.catalog.complete'),
    ('shops', 'inventory'),
    ('shops', 'currency.ledger'),
    ('warehouse', 'inventory'),
    ('items.enhancement', 'inventory'),
    ('items.enhancement', 'equipment.stats'),
    ('monsters', 'world.catalog.complete'),
    ('combat.pk', 'equipment.stats'),
    ('combat.pk', 'monsters'),
    ('drops', 'inventory'),
    ('drops', 'combat.pk'),
    ('skills', 'equipment.stats'),
    ('skills', 'combat.pk'),
    ('progression.rebirth', 'skills'),
    ('social.basic', 'world.catalog.complete'),
    ('trade.personalshops', 'social.basic'),
    ('trade.personalshops', 'inventory'),
    ('trade.personalshops', 'currency.ledger'),
    ('guild.marriage', 'social.basic'),
    ('guild.marriage', 'currency.ledger'),
    ('npc.quests', 'world.catalog.complete'),
    ('npc.quests', 'inventory'),
    ('events.arena.rankings', 'npc.quests'),
    ('events.arena.rankings', 'combat.pk'),
    ('housing.mining.weather', 'world.catalog.complete'),
    ('housing.mining.weather', 'inventory'),
    ('chat.gm', 'social.basic')
ON CONFLICT DO NOTHING;

GRANT USAGE ON SCHEMA gameplay TO revora_game, revora_backup;
GRANT SELECT ON gameplay.feature_gates, gameplay.feature_gate_dependencies TO revora_game, revora_backup;
GRANT SELECT ON gameplay_reference.source_artifacts,
    gameplay_reference.import_batches,
    gameplay_reference.catalog_releases,
    gameplay_reference.catalog_release_categories TO revora_game, revora_backup;

COMMIT;
