BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE gameplay_reference.quest_definitions (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    quest_id bigint NOT NULL CHECK (quest_id > 0),
    safe_name text NOT NULL CHECK (length(safe_name) BETWEEN 1 AND 160 AND safe_name !~ '[[:cntrl:]]'),
    minimum_level smallint NOT NULL DEFAULT 1 CHECK (minimum_level BETWEEN 1 AND 255),
    maximum_level smallint NULL CHECK (maximum_level BETWEEN 1 AND 255),
    repeat_policy text NOT NULL CHECK (repeat_policy IN ('once', 'daily', 'weekly', 'repeatable')),
    enabled boolean NOT NULL DEFAULT false,
    canonical_record_sha256 text NOT NULL CHECK (canonical_record_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (source_id, quest_id),
    CHECK (maximum_level IS NULL OR maximum_level >= minimum_level)
);

CREATE TABLE gameplay_reference.quest_steps (
    source_id uuid NOT NULL,
    quest_id bigint NOT NULL,
    step_number smallint NOT NULL CHECK (step_number BETWEEN 1 AND 32767),
    objective_kind text NOT NULL CHECK (objective_kind IN
        ('talk-to-npc', 'visit-map-cell', 'collect-item', 'defeat-monster', 'deliver-item')),
    target_legacy_id bigint NOT NULL CHECK (target_legacy_id > 0),
    required_count integer NOT NULL DEFAULT 1 CHECK (required_count BETWEEN 1 AND 1000000),
    PRIMARY KEY (source_id, quest_id, step_number),
    FOREIGN KEY (source_id, quest_id)
        REFERENCES gameplay_reference.quest_definitions(source_id, quest_id) ON DELETE CASCADE
);

CREATE TABLE gameplay_reference.npc_declarative_actions (
    source_id uuid NOT NULL,
    npc_legacy_uid bigint NOT NULL,
    option_id integer NOT NULL CHECK (option_id BETWEEN 0 AND 65535),
    action_kind text NOT NULL CHECK (action_kind IN
        ('close-dialog', 'show-dialog', 'open-shop', 'open-warehouse', 'teleport', 'start-quest', 'advance-quest')),
    target_legacy_id bigint NULL CHECK (target_legacy_id > 0),
    safe_text text NOT NULL DEFAULT '' CHECK (length(safe_text) <= 1024 AND safe_text !~ '[[:cntrl:]]'),
    canonical_record_sha256 text NOT NULL CHECK (canonical_record_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (source_id, npc_legacy_uid, option_id),
    FOREIGN KEY (source_id, npc_legacy_uid)
        REFERENCES gameplay_reference.npcs(source_id, legacy_uid)
);

CREATE TABLE game.character_quest_progress (
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    quest_source_id uuid NOT NULL,
    quest_id bigint NOT NULL,
    step_number smallint NOT NULL DEFAULT 1 CHECK (step_number BETWEEN 1 AND 32767),
    progress_count integer NOT NULL DEFAULT 0 CHECK (progress_count >= 0),
    state text NOT NULL CHECK (state IN ('active', 'completed', 'abandoned')),
    started_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    completed_at timestamptz NULL,
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version > 0),
    PRIMARY KEY (character_id, quest_id),
    FOREIGN KEY (quest_source_id, quest_id)
        REFERENCES gameplay_reference.quest_definitions(source_id, quest_id),
    CHECK ((state = 'completed') = (completed_at IS NOT NULL))
);

GRANT SELECT ON gameplay_reference.quest_definitions,
    gameplay_reference.quest_steps, gameplay_reference.npc_declarative_actions TO revora_game, revora_backup;
GRANT SELECT, INSERT, UPDATE, DELETE ON game.character_quest_progress TO revora_game;
GRANT SELECT ON game.character_quest_progress TO revora_backup;

COMMIT;
