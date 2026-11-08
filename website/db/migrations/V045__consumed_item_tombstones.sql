BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE game.consumed_item_instances (
    item_instance_id uuid PRIMARY KEY REFERENCES game.item_instances(item_instance_id) ON DELETE RESTRICT,
    owner_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    operation_id uuid NOT NULL UNIQUE REFERENCES game.gameplay_command_receipts(command_id) ON DELETE RESTRICT,
    consumed_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    FOREIGN KEY (item_instance_id, owner_character_id)
        REFERENCES game.item_instances(item_instance_id, owner_character_id) ON DELETE RESTRICT
);

CREATE OR REPLACE FUNCTION game.reject_ground_item_multi_placement() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM game.world_ground_drops d
               WHERE d.item_instance_id=NEW.item_instance_id AND d.state IN ('active','expired')) THEN
        RAISE EXCEPTION 'item instance is active or expired on the ground';
    END IF;
    IF EXISTS (SELECT 1 FROM game.consumed_item_instances c
               WHERE c.item_instance_id=NEW.item_instance_id) THEN
        RAISE EXCEPTION 'item instance has already been consumed';
    END IF;
    RETURN NEW;
END $$;

GRANT SELECT, INSERT ON game.consumed_item_instances TO revora_game;
GRANT SELECT ON game.consumed_item_instances TO revora_backup;

COMMIT;
