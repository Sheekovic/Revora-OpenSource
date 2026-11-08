BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE game.character_wallets (
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    currency_type text NOT NULL CHECK (currency_type IN ('silver', 'conquer-points', 'bound-conquer-points')),
    balance bigint NOT NULL DEFAULT 0 CHECK (balance >= 0),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version > 0),
    PRIMARY KEY (character_id, currency_type)
);

CREATE TABLE game.currency_ledger (
    ledger_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    operation_id uuid NOT NULL,
    character_id uuid NOT NULL,
    currency_type text NOT NULL,
    delta bigint NOT NULL CHECK (delta <> 0),
    balance_after bigint NOT NULL CHECK (balance_after >= 0),
    reason text NOT NULL CHECK (reason IN (
        'shop-buy', 'shop-sell', 'trade', 'personal-shop', 'quest', 'event',
        'monster-drop', 'repair', 'upgrade', 'gameplay-adjustment',
        'admin-adjustment', 'system-grant')),
    correlation_id uuid NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    UNIQUE (character_id, currency_type, operation_id),
    FOREIGN KEY (character_id, currency_type)
        REFERENCES game.character_wallets(character_id, currency_type)
);

CREATE FUNCTION game.reject_currency_ledger_mutation() RETURNS trigger
LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'currency ledger entries are append-only'; END $$;
CREATE TRIGGER currency_ledger_append_only
BEFORE UPDATE OR DELETE ON game.currency_ledger
FOR EACH ROW EXECUTE FUNCTION game.reject_currency_ledger_mutation();

CREATE INDEX currency_ledger_character_time_idx
    ON game.currency_ledger(character_id, occurred_at DESC, ledger_id DESC);

GRANT SELECT, INSERT, UPDATE ON game.character_wallets TO revora_game;
GRANT SELECT, INSERT ON game.currency_ledger TO revora_game;
GRANT USAGE, SELECT ON SEQUENCE game.currency_ledger_ledger_id_seq TO revora_game;
GRANT SELECT ON game.character_wallets, game.currency_ledger TO revora_backup;
GRANT SELECT ON SEQUENCE game.currency_ledger_ledger_id_seq TO revora_backup;

COMMIT;
