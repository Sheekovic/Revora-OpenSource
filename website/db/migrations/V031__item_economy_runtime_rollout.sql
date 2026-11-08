BEGIN;
SET LOCAL ROLE revora_migrator;

ALTER TABLE gameplay_reference.item_types
    ADD COLUMN required_strength integer NOT NULL DEFAULT 0 CHECK (required_strength BETWEEN 0 AND 65535),
    ADD COLUMN required_agility integer NOT NULL DEFAULT 0 CHECK (required_agility BETWEEN 0 AND 65535);

ALTER TABLE game.characters
    ADD CONSTRAINT characters_identity_account_uq UNIQUE(character_id, account_id),
    ADD CONSTRAINT characters_identity_legacy_uid_uq UNIQUE(character_id, legacy_uid);

CREATE TABLE gameplay.feature_rollouts (
    feature_key text PRIMARY KEY REFERENCES gameplay.feature_gates(feature_key) ON DELETE CASCADE,
    rollout_scope text NOT NULL CHECK (rollout_scope IN ('disabled', 'canary', 'global')),
    changed_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    changed_by text NOT NULL CHECK (length(changed_by) BETWEEN 1 AND 100)
);

CREATE TABLE gameplay.feature_canaries (
    feature_key text NOT NULL REFERENCES gameplay.feature_rollouts(feature_key) ON DELETE CASCADE,
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE CASCADE,
    character_id uuid NOT NULL,
    legacy_character_uid integer NOT NULL CHECK (legacy_character_uid > 0),
    enabled_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    enabled_by text NOT NULL CHECK (length(enabled_by) BETWEEN 1 AND 100),
    PRIMARY KEY (feature_key, account_id, character_id),
    FOREIGN KEY (character_id, account_id)
        REFERENCES game.characters(character_id, account_id) ON DELETE CASCADE,
    FOREIGN KEY (character_id, legacy_character_uid)
        REFERENCES game.characters(character_id, legacy_uid) ON DELETE CASCADE
);

INSERT INTO gameplay.feature_rollouts(feature_key,rollout_scope,changed_by)
SELECT feature_key,'disabled','migration-v031'
FROM gameplay.feature_gates
ON CONFLICT (feature_key) DO NOTHING;

CREATE TABLE operations.owner_test_grant_events (
    operation_id uuid NOT NULL,
    event_type text NOT NULL CHECK (event_type IN ('grant', 'rollback')),
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE RESTRICT,
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    legacy_character_uid integer NOT NULL CHECK (legacy_character_uid > 0),
    item_instance_id uuid NOT NULL,
    legacy_item_uid integer NOT NULL CHECK (legacy_item_uid > 0),
    item_type bigint NOT NULL CHECK (item_type > 0),
    silver_before bigint NOT NULL CHECK (silver_before >= 0),
    silver_after bigint NOT NULL CHECK (silver_after >= 0),
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY (operation_id,event_type),
    FOREIGN KEY (character_id,account_id) REFERENCES game.characters(character_id,account_id),
    FOREIGN KEY (character_id,legacy_character_uid) REFERENCES game.characters(character_id,legacy_uid)
);

CREATE TRIGGER owner_test_grant_events_append_only
BEFORE UPDATE OR DELETE ON operations.owner_test_grant_events
FOR EACH ROW EXECUTE FUNCTION operations.reject_audit_mutation();

GRANT SELECT ON gameplay.feature_rollouts, gameplay.feature_canaries TO revora_game, revora_backup;
GRANT SELECT, INSERT ON operations.owner_test_grant_events TO revora_game;
GRANT SELECT ON operations.owner_test_grant_events TO revora_backup;

COMMIT;
