BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE game.character_attributes (
    character_id uuid PRIMARY KEY REFERENCES game.characters(character_id) ON DELETE CASCADE,
    strength integer NOT NULL DEFAULT 0 CHECK (strength BETWEEN 0 AND 65535),
    agility integer NOT NULL DEFAULT 0 CHECK (agility BETWEEN 0 AND 65535),
    vitality integer NOT NULL DEFAULT 0 CHECK (vitality BETWEEN 0 AND 65535),
    spirit integer NOT NULL DEFAULT 0 CHECK (spirit BETWEEN 0 AND 65535),
    available_attribute_points integer NOT NULL DEFAULT 0 CHECK (available_attribute_points BETWEEN 0 AND 1000000),
    experience bigint NOT NULL DEFAULT 0 CHECK (experience >= 0),
    stamina smallint NOT NULL DEFAULT 100 CHECK (stamina BETWEEN 0 AND 100),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version > 0)
);

CREATE TABLE game.character_vitals (
    character_id uuid PRIMARY KEY REFERENCES game.characters(character_id) ON DELETE CASCADE,
    current_health integer NOT NULL DEFAULT 1 CHECK (current_health >= 0),
    current_mana integer NOT NULL DEFAULT 0 CHECK (current_mana >= 0),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version > 0)
);

CREATE TABLE game.character_stat_snapshots (
    character_id uuid PRIMARY KEY REFERENCES game.characters(character_id) ON DELETE CASCADE,
    character_row_version bigint NOT NULL CHECK (character_row_version > 0),
    inventory_revision bigint NOT NULL CHECK (inventory_revision >= 0),
    maximum_health integer NOT NULL CHECK (maximum_health > 0),
    maximum_mana integer NOT NULL CHECK (maximum_mana >= 0),
    minimum_attack integer NOT NULL CHECK (minimum_attack >= 0),
    maximum_attack integer NOT NULL CHECK (maximum_attack >= minimum_attack),
    defense integer NOT NULL CHECK (defense >= 0),
    magic_attack integer NOT NULL CHECK (magic_attack >= 0),
    magic_defense integer NOT NULL CHECK (magic_defense >= 0),
    accuracy integer NOT NULL CHECK (accuracy >= 0),
    dodge integer NOT NULL CHECK (dodge >= 0),
    calculated_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE VIEW game.equipped_items AS
SELECT i.owner_character_id AS character_id, l.slot, i.item_instance_id,
       i.legacy_item_uid, i.item_source_id, i.item_type, i.plus, i.bless,
       i.enchant, i.socket_one, i.socket_two, i.durability, i.maximum_durability,
       i.row_version AS item_row_version, l.row_version AS location_row_version
FROM game.item_instances i
JOIN game.item_locations l USING (item_instance_id, owner_character_id)
WHERE l.location_kind = 'equipment';

GRANT SELECT, INSERT, UPDATE ON game.character_attributes,
    game.character_vitals, game.character_stat_snapshots TO revora_game;
GRANT SELECT ON game.equipped_items TO revora_game;
GRANT SELECT ON game.character_attributes, game.character_vitals,
    game.character_stat_snapshots, game.equipped_items TO revora_backup;

COMMIT;
