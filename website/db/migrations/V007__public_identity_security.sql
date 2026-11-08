BEGIN;
SET LOCAL ROLE revora_migrator;

ALTER TABLE identity.game_accounts ADD COLUMN normalized_username text;
UPDATE identity.game_accounts SET normalized_username = lower(username);
ALTER TABLE identity.game_accounts ALTER COLUMN normalized_username SET NOT NULL;
ALTER TABLE identity.game_accounts ADD COLUMN security_stamp uuid NOT NULL DEFAULT gen_random_uuid();
ALTER TABLE identity.game_accounts ADD COLUMN public_registration boolean NOT NULL DEFAULT false;
ALTER TABLE identity.game_accounts ADD CONSTRAINT game_accounts_public_username_ck
    CHECK (NOT public_registration OR (username ~ '^[A-Za-z0-9]{4,15}$' AND normalized_username = lower(username)));
CREATE UNIQUE INDEX game_accounts_normalized_username_uq ON identity.game_accounts(normalized_username);

CREATE TABLE identity.account_roles (
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE CASCADE,
    role text NOT NULL CHECK (role = 'Player'),
    PRIMARY KEY(account_id, role)
);
INSERT INTO identity.account_roles(account_id, role)
SELECT account_id, 'Player' FROM identity.game_accounts ON CONFLICT DO NOTHING;

CREATE TABLE identity.account_security (
    account_id uuid PRIMARY KEY REFERENCES identity.game_accounts(account_id) ON DELETE CASCADE,
    recovery_generation integer NOT NULL DEFAULT 1 CHECK (recovery_generation > 0),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
INSERT INTO identity.account_security(account_id) SELECT account_id FROM identity.game_accounts ON CONFLICT DO NOTHING;

CREATE TABLE identity.recovery_codes (
    code_id uuid PRIMARY KEY,
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE CASCADE,
    generation integer NOT NULL CHECK (generation > 0),
    code_digest bytea NOT NULL UNIQUE CHECK (octet_length(code_digest) = 32),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    consumed_at timestamptz
);
CREATE INDEX recovery_codes_available_idx ON identity.recovery_codes(account_id, generation) WHERE consumed_at IS NULL;

CREATE TABLE identity.security_answers (
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE CASCADE,
    question_id smallint NOT NULL CHECK (question_id BETWEEN 1 AND 8),
    answer_hash text NOT NULL CHECK (length(answer_hash) BETWEEN 32 AND 1024),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY(account_id, question_id)
);

CREATE TABLE identity.recognized_devices (
    device_id uuid PRIMARY KEY,
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE CASCADE,
    token_digest bytea NOT NULL UNIQUE CHECK (octet_length(token_digest) = 32),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    last_seen_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    expires_at timestamptz NOT NULL,
    revoked_at timestamptz,
    CHECK (expires_at > created_at)
);

CREATE TABLE identity.network_history (
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE CASCADE,
    prefix_digest bytea NOT NULL CHECK (octet_length(prefix_digest) = 32),
    first_seen_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    last_seen_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    expires_at timestamptz NOT NULL,
    PRIMARY KEY(account_id, prefix_digest)
);

CREATE TABLE identity.web_sessions (
    session_digest bytea PRIMARY KEY CHECK (octet_length(session_digest) = 32),
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE CASCADE,
    security_stamp uuid NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    last_seen_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    expires_at timestamptz NOT NULL,
    revoked_at timestamptz,
    CHECK (expires_at > created_at)
);
CREATE INDEX web_sessions_account_active_idx ON identity.web_sessions(account_id, expires_at) WHERE revoked_at IS NULL;

CREATE TABLE identity.reset_authorizations (
    token_digest bytea PRIMARY KEY CHECK (octet_length(token_digest) = 32),
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE CASCADE,
    reason text NOT NULL CHECK (reason IN ('recovery-code','questions-device','admin-approved')),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    expires_at timestamptz NOT NULL,
    consumed_at timestamptz,
    CHECK (expires_at > created_at)
);

CREATE TABLE identity.pending_recoveries (
    request_id uuid PRIMARY KEY,
    request_digest bytea NOT NULL UNIQUE CHECK (octet_length(request_digest) = 32),
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE CASCADE,
    support_category text NOT NULL CHECK (support_category IN ('questions-device','questions-network','insufficient')),
    status text NOT NULL DEFAULT 'pending' CHECK (status IN ('pending','approved','rejected','expired','consumed')),
    requested_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    available_after timestamptz NOT NULL,
    expires_at timestamptz NOT NULL,
    reviewed_at timestamptz,
    reviewed_by uuid REFERENCES admin.administrators(admin_id),
    CHECK (expires_at > requested_at)
);

GRANT USAGE ON SCHEMA identity, game, operations TO revora_web;
GRANT SELECT, INSERT, UPDATE ON identity.game_accounts, identity.account_roles, identity.account_security,
    identity.recovery_codes, identity.security_answers, identity.recognized_devices, identity.network_history,
    identity.web_sessions, identity.reset_authorizations, identity.pending_recoveries TO revora_web;
GRANT SELECT, UPDATE ON identity.handoff_tokens, identity.launch_tickets TO revora_web;
GRANT DELETE ON identity.security_answers TO revora_web;
GRANT SELECT ON game.characters TO revora_web;
GRANT SELECT, INSERT ON operations.audit_events TO revora_web;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA operations TO revora_web;

GRANT SELECT, UPDATE ON identity.pending_recoveries TO revora_admin;
GRANT SELECT ON identity.account_security TO revora_admin;

COMMIT;
