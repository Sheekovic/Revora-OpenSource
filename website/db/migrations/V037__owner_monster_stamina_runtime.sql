BEGIN;
SET LOCAL ROLE revora_migrator;

ALTER TABLE gameplay_reference.monster_types
    ADD COLUMN damage_reduction smallint NOT NULL DEFAULT 1 CHECK (damage_reduction BETWEEN 1 AND 255),
    ADD COLUMN magic_skill integer NOT NULL DEFAULT 0 CHECK (magic_skill BETWEEN 0 AND 65535),
    ADD COLUMN magic_level smallint NOT NULL DEFAULT 0 CHECK (magic_level BETWEEN 0 AND 255),
    ADD COLUMN gives_rewards boolean NOT NULL DEFAULT true,
    ADD COLUMN minimum_silver integer NOT NULL DEFAULT 0 CHECK (minimum_silver >= 0),
    ADD COLUMN maximum_silver integer NOT NULL DEFAULT 0 CHECK (maximum_silver >= minimum_silver),
    ADD COLUMN use_level_difference_damage boolean NOT NULL DEFAULT true;

ALTER TABLE gameplay_reference.monster_spawn_regions
    ADD COLUMN activation_category text NOT NULL DEFAULT 'event-gated'
        CHECK (activation_category IN ('normal','event-gated','incompatible'));

CREATE TABLE game.monster_reward_receipts (
    operation_id uuid PRIMARY KEY,
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    monster_legacy_uid bigint NOT NULL CHECK (monster_legacy_uid > 0),
    monster_type integer NOT NULL CHECK (monster_type > 0),
    experience_awarded bigint NOT NULL CHECK (experience_awarded >= 0),
    skill_experience_awarded bigint NOT NULL CHECK (skill_experience_awarded >= 0),
    silver_awarded bigint NOT NULL CHECK (silver_awarded >= 0),
    item_instance_id uuid NULL REFERENCES game.item_instances(item_instance_id) ON DELETE RESTRICT,
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE game.monster_attack_receipts (
    operation_id uuid PRIMARY KEY,
    target_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    monster_legacy_uid bigint NOT NULL CHECK (monster_legacy_uid > 0),
    monster_type integer NOT NULL CHECK (monster_type > 0),
    previous_health integer NOT NULL CHECK (previous_health >= 0),
    resulting_health integer NOT NULL CHECK (resulting_health >= 0),
    damage integer NOT NULL CHECK (damage > 0),
    killed boolean NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE game.stamina_audit_receipts (
    operation_id uuid PRIMARY KEY,
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    previous_stamina smallint NOT NULL CHECK (previous_stamina BETWEEN 0 AND 100),
    resulting_stamina smallint NOT NULL CHECK (resulting_stamina BETWEEN 0 AND 100),
    reason text NOT NULL CHECK (reason IN ('regeneration','skill','revive','sitting-hit')),
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

-- The bespoke V035 canary row is retired. Standard generator rows are activated
-- only by the reviewed importer after DMap and event classification succeeds.
UPDATE gameplay_reference.live_monster_spawn_points
SET enabled=false
WHERE legacy_uid=400000035 AND monster_type=1 AND map_id=1002;

UPDATE gameplay.feature_gates SET required_migration=37 WHERE feature_key IN ('combat.runtime','ninja.native');

GRANT SELECT, INSERT ON game.monster_reward_receipts, game.monster_attack_receipts,
    game.stamina_audit_receipts TO revora_game;
GRANT SELECT ON game.monster_reward_receipts, game.monster_attack_receipts,
    game.stamina_audit_receipts TO revora_backup;

COMMIT;
