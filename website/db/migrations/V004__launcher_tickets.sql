BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE identity.launch_tickets (
    ticket_digest bytea PRIMARY KEY CHECK (octet_length(ticket_digest) = 32),
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id),
    realm_id uuid NOT NULL REFERENCES game.realms(realm_id),
    launcher_session_id uuid NOT NULL,
    client_build_id text NOT NULL CHECK (length(client_build_id) BETWEEN 1 AND 128),
    intended_launch_id uuid NOT NULL UNIQUE,
    issued_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    expires_at timestamptz NOT NULL,
    consumed_at timestamptz,
    CHECK (expires_at > issued_at)
);

CREATE INDEX launch_tickets_account_active_idx
    ON identity.launch_tickets(account_id, expires_at)
    WHERE consumed_at IS NULL;

GRANT SELECT, INSERT, UPDATE ON identity.launch_tickets TO revora_admin;
GRANT SELECT, UPDATE ON identity.launch_tickets TO revora_game;
GRANT SELECT ON identity.account_bans TO revora_game;

COMMIT;
