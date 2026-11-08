BEGIN;
SET LOCAL ROLE revora_migrator;

ALTER TABLE game.item_locations DROP CONSTRAINT item_locations_location_kind_check;
ALTER TABLE game.item_locations ADD CONSTRAINT item_locations_location_kind_check
    CHECK (location_kind IN ('inventory','equipment','trade','personal-shop','gm-discarded'));

CREATE TABLE game.gm_inventory_clear_receipts (
    operation_id uuid PRIMARY KEY,
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE RESTRICT,
    character_id uuid NOT NULL,
    legacy_character_uid integer NOT NULL CHECK (legacy_character_uid > 0),
    removed_item_uids integer[] NOT NULL,
    removed_count integer NOT NULL CHECK (removed_count >= 0),
    safe_category text NOT NULL CHECK (safe_category IN ('committed','empty-inventory')),
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CHECK (removed_count = cardinality(removed_item_uids)),
    CHECK (array_position(removed_item_uids,NULL) IS NULL),
    FOREIGN KEY (character_id,account_id) REFERENCES game.characters(character_id,account_id) ON DELETE RESTRICT,
    FOREIGN KEY (character_id,legacy_character_uid) REFERENCES game.characters(character_id,legacy_uid) ON DELETE RESTRICT
);

CREATE TRIGGER gm_inventory_clear_receipts_append_only
BEFORE UPDATE OR DELETE ON game.gm_inventory_clear_receipts
FOR EACH ROW EXECUTE FUNCTION operations.reject_audit_mutation();

GRANT SELECT,INSERT ON game.gm_inventory_clear_receipts TO revora_game;
GRANT SELECT ON game.gm_inventory_clear_receipts TO revora_backup;

COMMIT;
