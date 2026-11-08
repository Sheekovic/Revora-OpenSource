BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE game.trade_sessions (
    trade_id uuid PRIMARY KEY,
    first_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    second_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    state text NOT NULL CHECK (state IN ('open', 'first-confirmed', 'second-confirmed', 'committed', 'cancelled')),
    expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    completed_at timestamptz NULL,
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version > 0),
    CHECK (first_character_id <> second_character_id),
    CHECK (expires_at > created_at),
    CHECK ((state IN ('committed', 'cancelled')) = (completed_at IS NOT NULL))
);

CREATE TABLE game.trade_items (
    trade_id uuid NOT NULL REFERENCES game.trade_sessions(trade_id) ON DELETE CASCADE,
    offering_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    item_instance_id uuid NOT NULL,
    expected_item_version bigint NOT NULL CHECK (expected_item_version > 0),
    PRIMARY KEY (trade_id, item_instance_id),
    FOREIGN KEY (item_instance_id, offering_character_id)
        REFERENCES game.item_instances(item_instance_id, owner_character_id)
);

CREATE TABLE game.trade_currency_offers (
    trade_id uuid NOT NULL REFERENCES game.trade_sessions(trade_id) ON DELETE CASCADE,
    offering_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    currency_type text NOT NULL CHECK (currency_type IN ('silver', 'conquer-points', 'bound-conquer-points')),
    amount bigint NOT NULL CHECK (amount >= 0),
    PRIMARY KEY (trade_id, offering_character_id, currency_type)
);

CREATE TABLE game.personal_shops (
    personal_shop_id uuid PRIMARY KEY,
    owner_character_id uuid NOT NULL UNIQUE REFERENCES game.characters(character_id) ON DELETE CASCADE,
    map_id integer NOT NULL CHECK (map_id > 0),
    x integer NOT NULL CHECK (x BETWEEN 0 AND 65535),
    y integer NOT NULL CHECK (y BETWEEN 0 AND 65535),
    state text NOT NULL CHECK (state IN ('open', 'closed')),
    opened_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    closed_at timestamptz NULL,
    CHECK ((state = 'closed') = (closed_at IS NOT NULL))
);

CREATE TABLE game.personal_shop_entries (
    personal_shop_id uuid NOT NULL REFERENCES game.personal_shops(personal_shop_id) ON DELETE CASCADE,
    item_instance_id uuid NOT NULL UNIQUE REFERENCES game.item_instances(item_instance_id) ON DELETE RESTRICT,
    currency_type text NOT NULL CHECK (currency_type IN ('silver', 'conquer-points')),
    asking_price bigint NOT NULL CHECK (asking_price > 0),
    PRIMARY KEY (personal_shop_id, item_instance_id)
);

GRANT SELECT, INSERT, UPDATE, DELETE ON game.trade_sessions, game.trade_items,
    game.trade_currency_offers, game.personal_shops, game.personal_shop_entries TO revora_game;
GRANT SELECT ON game.trade_sessions, game.trade_items, game.trade_currency_offers,
    game.personal_shops, game.personal_shop_entries TO revora_backup;

COMMIT;
