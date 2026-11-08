BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE SCHEMA IF NOT EXISTS operations AUTHORIZATION revora_migrator;
CREATE SCHEMA IF NOT EXISTS identity AUTHORIZATION revora_migrator;
CREATE SCHEMA IF NOT EXISTS game AUTHORIZATION revora_migrator;
CREATE SCHEMA IF NOT EXISTS admin AUTHORIZATION revora_migrator;

CREATE TABLE operations.schema_migrations (
    version integer PRIMARY KEY,
    name text NOT NULL,
    checksum_sha256 char(64) NOT NULL CHECK (checksum_sha256 ~ '^[0-9a-f]{64}$'),
    applied_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE identity.game_accounts (
    account_id uuid PRIMARY KEY,
    username text NOT NULL CHECK (length(username) BETWEEN 3 AND 32),
    password_hash text NOT NULL CHECK (length(password_hash) BETWEEN 32 AND 1024),
    password_algorithm text NOT NULL,
    password_version smallint NOT NULL CHECK (password_version > 0),
    status text NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'disabled', 'locked', 'banned')),
    failed_login_count integer NOT NULL DEFAULT 0 CHECK (failed_login_count >= 0),
    locked_until timestamptz,
    force_password_reset boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    row_version bigint NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX game_accounts_username_ci_uq ON identity.game_accounts (lower(username));

CREATE TABLE game.realms (
    realm_id uuid PRIMARY KEY,
    name text NOT NULL UNIQUE CHECK (length(name) BETWEEN 1 AND 64),
    protocol_patch integer NOT NULL CHECK (protocol_patch > 0),
    maintenance boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE identity.handoff_tokens (
    token_digest bytea PRIMARY KEY CHECK (octet_length(token_digest) = 32),
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE CASCADE,
    realm_id uuid NOT NULL REFERENCES game.realms(realm_id) ON DELETE CASCADE,
    expires_at timestamptz NOT NULL,
    consumed_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE INDEX handoff_tokens_expiry_idx ON identity.handoff_tokens (expires_at) WHERE consumed_at IS NULL;

CREATE TABLE identity.game_sessions (
    session_id uuid PRIMARY KEY,
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE CASCADE,
    realm_id uuid NOT NULL REFERENCES game.realms(realm_id) ON DELETE CASCADE,
    connected_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    disconnected_at timestamptz,
    disconnect_reason text,
    connection_id uuid NOT NULL,
    CONSTRAINT game_sessions_disconnect_pair CHECK ((disconnected_at IS NULL) = (disconnect_reason IS NULL))
);
CREATE UNIQUE INDEX one_live_session_per_account ON identity.game_sessions(account_id) WHERE disconnected_at IS NULL;

CREATE TABLE game.characters (
    character_id uuid PRIMARY KEY,
    account_id uuid NOT NULL UNIQUE REFERENCES identity.game_accounts(account_id) ON DELETE RESTRICT,
    realm_id uuid NOT NULL REFERENCES game.realms(realm_id) ON DELETE RESTRICT,
    name text NOT NULL CHECK (length(name) BETWEEN 3 AND 16),
    map_id integer NOT NULL CHECK (map_id > 0),
    x integer NOT NULL CHECK (x BETWEEN 0 AND 65535),
    y integer NOT NULL CHECK (y BETWEEN 0 AND 65535),
    direction smallint NOT NULL DEFAULT 0 CHECK (direction BETWEEN 0 AND 7),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    row_version bigint NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX characters_name_ci_uq ON game.characters (lower(name));

CREATE TABLE identity.account_bans (
    ban_id uuid PRIMARY KEY,
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE CASCADE,
    reason text NOT NULL CHECK (length(reason) BETWEEN 3 AND 1000),
    actor_admin_id uuid NOT NULL,
    starts_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    expires_at timestamptz,
    revoked_at timestamptz,
    revoked_by uuid,
    CHECK (expires_at IS NULL OR expires_at > starts_at)
);

CREATE TABLE admin.administrators (
    admin_id uuid PRIMARY KEY,
    username text NOT NULL CHECK (length(username) BETWEEN 3 AND 64),
    password_hash text NOT NULL CHECK (length(password_hash) BETWEEN 32 AND 1024),
    password_algorithm text NOT NULL,
    password_version smallint NOT NULL CHECK (password_version > 0),
    totp_secret_ciphertext bytea,
    totp_enabled boolean NOT NULL DEFAULT false,
    must_change_password boolean NOT NULL DEFAULT true,
    must_enroll_mfa boolean NOT NULL DEFAULT true,
    status text NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'disabled', 'locked')),
    failed_login_count integer NOT NULL DEFAULT 0 CHECK (failed_login_count >= 0),
    locked_until timestamptz,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    row_version bigint NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX administrators_username_ci_uq ON admin.administrators (lower(username));

CREATE TABLE admin.admin_sessions (
    session_digest bytea PRIMARY KEY CHECK (octet_length(session_digest) = 32),
    admin_id uuid NOT NULL REFERENCES admin.administrators(admin_id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    last_seen_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    absolute_expires_at timestamptz NOT NULL,
    revoked_at timestamptz,
    CHECK (absolute_expires_at > created_at)
);

CREATE TABLE operations.audit_events (
    audit_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    actor_type text NOT NULL,
    actor_id uuid,
    action text NOT NULL,
    target_type text NOT NULL,
    target_id text NOT NULL,
    reason text,
    correlation_id uuid NOT NULL,
    before_summary jsonb,
    after_summary jsonb
);

CREATE FUNCTION operations.reject_audit_mutation() RETURNS trigger
LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'audit events are append-only'; END $$;
CREATE TRIGGER audit_events_append_only
BEFORE UPDATE OR DELETE ON operations.audit_events
FOR EACH ROW EXECUTE FUNCTION operations.reject_audit_mutation();

CREATE TABLE operations.backup_status (
    backup_name text PRIMARY KEY,
    completed_at timestamptz,
    checksum_sha256 char(64),
    restore_tested_at timestamptz,
    status text NOT NULL CHECK (status IN ('never', 'success', 'failed')),
    safe_detail text NOT NULL
);

REVOKE ALL ON SCHEMA identity, game, operations, admin FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA identity, game, operations, admin FROM PUBLIC;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA identity, game, operations, admin FROM PUBLIC;

GRANT USAGE ON SCHEMA identity, game, operations TO revora_game;
GRANT SELECT, INSERT, UPDATE ON identity.game_accounts, identity.handoff_tokens, identity.game_sessions TO revora_game;
GRANT SELECT, INSERT, UPDATE ON game.realms, game.characters TO revora_game;
GRANT SELECT, INSERT ON operations.audit_events TO revora_game;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA operations TO revora_game;

GRANT USAGE ON SCHEMA identity, game, operations, admin TO revora_admin;
GRANT SELECT, INSERT, UPDATE ON identity.game_accounts, identity.account_bans TO revora_admin;
GRANT SELECT ON identity.game_sessions, game.realms, game.characters, operations.schema_migrations, operations.backup_status TO revora_admin;
GRANT SELECT, INSERT ON operations.audit_events TO revora_admin;
GRANT SELECT, INSERT, UPDATE ON admin.administrators, admin.admin_sessions TO revora_admin;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA operations TO revora_admin;

GRANT USAGE ON SCHEMA game, operations TO revora_reporting;
GRANT SELECT ON ALL TABLES IN SCHEMA game, operations TO revora_reporting;

INSERT INTO game.realms(realm_id, name, protocol_patch, maintenance)
VALUES ('4d1e7aac-e046-48bd-88b9-642f7eafef83', 'Revora Development', 5165, true)
ON CONFLICT (realm_id) DO NOTHING;

COMMIT;
