BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE admin.gameplay_permissions (
    admin_id uuid NOT NULL REFERENCES admin.administrators(admin_id) ON DELETE CASCADE,
    permission text NOT NULL CHECK (permission IN
        ('player-teleport', 'player-moderate', 'event-control', 'economy-adjust', 'world-inspect')),
    granted_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    granted_by uuid NULL REFERENCES admin.administrators(admin_id) ON DELETE SET NULL,
    revoked_at timestamptz NULL,
    PRIMARY KEY (admin_id, permission)
);

CREATE TABLE game.chat_moderation_state (
    character_id uuid PRIMARY KEY REFERENCES game.characters(character_id) ON DELETE CASCADE,
    muted_until timestamptz NULL,
    reason_category text NULL CHECK (
        reason_category IS NULL OR reason_category ~ '^[a-z][a-z0-9-]{1,79}$'),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version > 0),
    CHECK ((muted_until IS NULL) = (reason_category IS NULL))
);

CREATE TABLE game.gameplay_admin_operations (
    operation_id uuid PRIMARY KEY,
    admin_id uuid NOT NULL REFERENCES admin.administrators(admin_id) ON DELETE RESTRICT,
    permission text NOT NULL CHECK (permission IN
        ('player-teleport', 'player-moderate', 'event-control', 'economy-adjust', 'world-inspect')),
    action_kind text NOT NULL CHECK (action_kind IN
        ('teleport', 'mute', 'unmute', 'start-event', 'stop-event', 'adjust-currency', 'inspect')),
    target_character_id uuid NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    safe_reason text NOT NULL CHECK (length(safe_reason) BETWEEN 3 AND 500 AND safe_reason !~ '[[:cntrl:]]'),
    result text NOT NULL CHECK (result IN ('committed', 'rejected')),
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    FOREIGN KEY (admin_id, permission)
        REFERENCES admin.gameplay_permissions(admin_id, permission)
);

GRANT SELECT ON admin.gameplay_permissions TO revora_game, revora_backup;
GRANT SELECT, INSERT, UPDATE ON game.chat_moderation_state TO revora_game;
GRANT SELECT, INSERT ON game.gameplay_admin_operations TO revora_game;
GRANT SELECT ON game.chat_moderation_state, game.gameplay_admin_operations TO revora_backup;

COMMIT;
