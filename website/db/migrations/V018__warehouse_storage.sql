BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE gameplay_reference.warehouse_definitions (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    legacy_warehouse_id bigint NOT NULL CHECK (legacy_warehouse_id > 0),
    npc_source_id uuid NOT NULL,
    npc_legacy_uid bigint NOT NULL,
    slot_capacity smallint NOT NULL CHECK (slot_capacity BETWEEN 1 AND 255),
    canonical_record_sha256 text NOT NULL CHECK (canonical_record_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (source_id, legacy_warehouse_id),
    FOREIGN KEY (npc_source_id, npc_legacy_uid)
        REFERENCES gameplay_reference.npcs(source_id, legacy_uid)
);

CREATE TABLE gameplay_reference.warehouse_release_entries (
    release_id uuid NOT NULL REFERENCES gameplay_reference.catalog_releases(release_id) ON DELETE CASCADE,
    legacy_warehouse_id bigint NOT NULL,
    source_id uuid NOT NULL,
    PRIMARY KEY (release_id, legacy_warehouse_id),
    FOREIGN KEY (source_id, legacy_warehouse_id)
        REFERENCES gameplay_reference.warehouse_definitions(source_id, legacy_warehouse_id)
);

CREATE TABLE game.warehouse_vaults (
    vault_id uuid PRIMARY KEY,
    owner_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    warehouse_source_id uuid NOT NULL,
    legacy_warehouse_id bigint NOT NULL,
    slot_capacity smallint NOT NULL CHECK (slot_capacity BETWEEN 1 AND 255),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version > 0),
    UNIQUE (vault_id, owner_character_id),
    UNIQUE (owner_character_id, warehouse_source_id, legacy_warehouse_id),
    FOREIGN KEY (warehouse_source_id, legacy_warehouse_id)
        REFERENCES gameplay_reference.warehouse_definitions(source_id, legacy_warehouse_id)
);

CREATE TABLE game.warehouse_items (
    vault_id uuid NOT NULL,
    owner_character_id uuid NOT NULL,
    slot smallint NOT NULL CHECK (slot BETWEEN 0 AND 254),
    item_instance_id uuid NOT NULL,
    placed_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY (vault_id, slot),
    UNIQUE (item_instance_id),
    FOREIGN KEY (vault_id, owner_character_id)
        REFERENCES game.warehouse_vaults(vault_id, owner_character_id) ON DELETE CASCADE,
    FOREIGN KEY (item_instance_id, owner_character_id)
        REFERENCES game.item_instances(item_instance_id, owner_character_id) ON DELETE CASCADE
);

CREATE FUNCTION game.enforce_single_item_placement() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    PERFORM 1 FROM game.item_instances
      WHERE item_instance_id = NEW.item_instance_id AND owner_character_id = NEW.owner_character_id
      FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'item ownership does not match the requested placement';
    END IF;
    IF TG_TABLE_NAME = 'warehouse_items' THEN
        IF EXISTS (SELECT 1 FROM game.item_locations WHERE item_instance_id = NEW.item_instance_id) THEN
            RAISE EXCEPTION 'item already has a non-warehouse placement';
        END IF;
    ELSE
        IF EXISTS (SELECT 1 FROM game.warehouse_items WHERE item_instance_id = NEW.item_instance_id) THEN
            RAISE EXCEPTION 'item already has a warehouse placement';
        END IF;
    END IF;
    RETURN NEW;
END
$$;

CREATE TRIGGER warehouse_items_single_placement
BEFORE INSERT OR UPDATE OF item_instance_id, owner_character_id ON game.warehouse_items
FOR EACH ROW EXECUTE FUNCTION game.enforce_single_item_placement();

CREATE TRIGGER item_locations_single_placement
BEFORE INSERT OR UPDATE OF item_instance_id, owner_character_id ON game.item_locations
FOR EACH ROW EXECUTE FUNCTION game.enforce_single_item_placement();

GRANT SELECT ON gameplay_reference.warehouse_definitions,
    gameplay_reference.warehouse_release_entries TO revora_game, revora_backup;
GRANT SELECT, INSERT, UPDATE, DELETE ON game.warehouse_vaults,
    game.warehouse_items TO revora_game;
GRANT SELECT ON game.warehouse_vaults, game.warehouse_items TO revora_backup;

COMMIT;
