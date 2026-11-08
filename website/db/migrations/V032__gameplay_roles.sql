BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE game.gameplay_role_assignments (
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE CASCADE,
    character_id uuid NOT NULL,
    role text NOT NULL CHECK (role IN ('gm')),
    granted_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    granted_by text NOT NULL CHECK (length(granted_by) BETWEEN 3 AND 100),
    revoked_at timestamptz NULL,
    PRIMARY KEY (account_id, character_id, role),
    FOREIGN KEY (character_id, account_id)
        REFERENCES game.characters(character_id, account_id) ON DELETE CASCADE
);

GRANT SELECT ON game.gameplay_role_assignments TO revora_game, revora_backup;

COMMIT;
