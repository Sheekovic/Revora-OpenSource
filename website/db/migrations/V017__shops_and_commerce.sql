BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE gameplay_reference.shop_catalogs (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    legacy_shop_id bigint NOT NULL CHECK (legacy_shop_id > 0),
    npc_source_id uuid NOT NULL,
    npc_legacy_uid bigint NOT NULL,
    safe_name text NOT NULL CHECK (
        length(safe_name) BETWEEN 1 AND 128 AND
        safe_name = btrim(safe_name) AND
        safe_name !~ '[[:cntrl:]]'),
    currency_type text NOT NULL CHECK (currency_type IN ('silver', 'conquer-points', 'bound-conquer-points')),
    purchases_enabled boolean NOT NULL DEFAULT true,
    sales_enabled boolean NOT NULL DEFAULT true,
    canonical_record_sha256 text NOT NULL CHECK (canonical_record_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (source_id, legacy_shop_id),
    FOREIGN KEY (npc_source_id, npc_legacy_uid)
        REFERENCES gameplay_reference.npcs(source_id, legacy_uid)
);

CREATE TABLE gameplay_reference.shop_catalog_items (
    source_id uuid NOT NULL,
    legacy_shop_id bigint NOT NULL,
    position smallint NOT NULL CHECK (position BETWEEN 0 AND 255),
    item_source_id uuid NOT NULL,
    item_type bigint NOT NULL,
    purchase_price bigint NOT NULL CHECK (purchase_price >= 0),
    sale_price bigint NOT NULL CHECK (sale_price >= 0),
    stock_limit integer NULL CHECK (stock_limit > 0),
    PRIMARY KEY (source_id, legacy_shop_id, position),
    UNIQUE (source_id, legacy_shop_id, item_type),
    FOREIGN KEY (source_id, legacy_shop_id)
        REFERENCES gameplay_reference.shop_catalogs(source_id, legacy_shop_id) ON DELETE CASCADE,
    FOREIGN KEY (item_source_id, item_type)
        REFERENCES gameplay_reference.item_types(source_id, item_type)
);

CREATE TABLE gameplay_reference.shop_release_entries (
    release_id uuid NOT NULL REFERENCES gameplay_reference.catalog_releases(release_id) ON DELETE CASCADE,
    legacy_shop_id bigint NOT NULL,
    source_id uuid NOT NULL,
    PRIMARY KEY (release_id, legacy_shop_id),
    FOREIGN KEY (source_id, legacy_shop_id)
        REFERENCES gameplay_reference.shop_catalogs(source_id, legacy_shop_id)
);

CREATE TABLE game.commerce_operations (
    operation_id uuid PRIMARY KEY,
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    shop_source_id uuid NOT NULL,
    legacy_shop_id bigint NOT NULL,
    operation_kind text NOT NULL CHECK (operation_kind IN ('buy', 'sell')),
    item_instance_id uuid NULL,
    item_source_id uuid NOT NULL,
    item_type bigint NOT NULL,
    quantity integer NOT NULL CHECK (quantity BETWEEN 1 AND 65535),
    currency_type text NOT NULL CHECK (currency_type IN ('silver', 'conquer-points', 'bound-conquer-points')),
    total_amount bigint NOT NULL CHECK (total_amount >= 0),
    result text NOT NULL CHECK (result IN ('committed', 'rejected')),
    safe_result_category text NOT NULL CHECK (
        length(safe_result_category) BETWEEN 1 AND 80 AND
        safe_result_category ~ '^[a-z][a-z0-9-]*$'),
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    FOREIGN KEY (shop_source_id, legacy_shop_id)
        REFERENCES gameplay_reference.shop_catalogs(source_id, legacy_shop_id),
    FOREIGN KEY (item_source_id, item_type)
        REFERENCES gameplay_reference.item_types(source_id, item_type),
    FOREIGN KEY (item_instance_id, character_id, item_source_id, item_type)
        REFERENCES game.item_instances(item_instance_id, owner_character_id, item_source_id, item_type)
        ON DELETE RESTRICT
);

CREATE INDEX commerce_operations_character_time_idx
    ON game.commerce_operations(character_id, occurred_at DESC);

GRANT SELECT ON gameplay_reference.shop_catalogs,
    gameplay_reference.shop_catalog_items,
    gameplay_reference.shop_release_entries TO revora_game, revora_backup;
GRANT SELECT, INSERT ON game.commerce_operations TO revora_game;
GRANT SELECT ON game.commerce_operations TO revora_backup;

COMMIT;
