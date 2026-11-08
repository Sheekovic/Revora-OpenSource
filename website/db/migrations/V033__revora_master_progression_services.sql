BEGIN;
SET LOCAL ROLE revora_migrator;

-- Revora-owned, reviewed map-1002 service placement. The selected cell is
-- accessible in the approved newplain.DMap (SHA-256 68a19eef...).
INSERT INTO gameplay_reference.import_sources
    (source_id, archive_sha256, content_sha256, classification)
VALUES
    ('51650033-1002-4e56-8000-000000000001',
     '431d7bf13f6a6d9f8d03b3401ac78d53240f3bed2f823363bd3750b07dceccde',
     'a22df409872646914d64ec562c5d152ada1163f3c8981f08ff6f7b17b9b58b0a',
     'reviewed-reference-data')
ON CONFLICT DO NOTHING;

INSERT INTO gameplay_reference.npcs
    (source_id,legacy_uid,npc_type,flags,face,map_id,x,y,active_month,active_begin,active_end,
     destination_map,destination_x,destination_y,duration)
VALUES
    ('51650033-1002-4e56-8000-000000000001',900000001,120,2,6,1002,349,359,0,0,0,0,0,0,0)
ON CONFLICT DO NOTHING;

ALTER TABLE game.character_progression
    ADD COLUMN original_profession smallint NOT NULL DEFAULT 0 CHECK (original_profession BETWEEN 0 AND 255),
    ADD COLUMN first_rebirth_profession smallint NOT NULL DEFAULT 0 CHECK (first_rebirth_profession BETWEEN 0 AND 255),
    ADD COLUMN second_rebirth_profession smallint NOT NULL DEFAULT 0 CHECK (second_rebirth_profession BETWEEN 0 AND 255);

ALTER TABLE game.characters DROP CONSTRAINT characters_profession_check;
ALTER TABLE game.characters ADD CONSTRAINT characters_profession_check CHECK
    (profession IN (10,11,20,21,40,41,50,100,132,142));

-- The exact 5165 client and authorized donor use bounded, signed-display-safe
-- values. Keeping the database at the same bound prevents later encoders from
-- accepting a value the genuine client cannot present safely.
ALTER TABLE game.character_wallets
    ADD CONSTRAINT character_wallets_exact_5165_cap CHECK (balance BETWEEN 0 AND 2000000000);

ALTER TABLE game.currency_ledger DROP CONSTRAINT currency_ledger_reason_check;
ALTER TABLE game.currency_ledger ADD CONSTRAINT currency_ledger_reason_check CHECK (reason IN (
    'shop-buy', 'shop-sell', 'trade', 'personal-shop', 'quest', 'event',
    'monster-drop', 'repair', 'upgrade', 'gameplay-adjustment',
    'admin-adjustment', 'system-grant', 'progression-service', 'gm-cap'));

CREATE TABLE gameplay_reference.progression_service_definitions (
    service_key text PRIMARY KEY CHECK (service_key IN
        ('maximum-level','first-rebirth','second-rebirth','class-loadout')),
    display_name text NOT NULL CHECK (length(display_name) BETWEEN 3 AND 80 AND display_name !~ '[[:cntrl:]]'),
    conquer_point_cost bigint NOT NULL CHECK (conquer_point_cost = 1000000),
    version integer NOT NULL CHECK (version > 0),
    enabled boolean NOT NULL DEFAULT true
);

INSERT INTO gameplay_reference.progression_service_definitions
    (service_key,display_name,conquer_point_cost,version)
VALUES
    ('maximum-level','Reach maximum level',1000000,1),
    ('first-rebirth','First Rebirth',1000000,1),
    ('second-rebirth','Second Rebirth',1000000,1),
    ('class-loadout','Get full class equipment',1000000,1);

CREATE TABLE gameplay_reference.class_loadouts (
    loadout_key text PRIMARY KEY CHECK (loadout_key ~ '^[a-z][a-z0-9-]{2,31}$'),
    display_name text NOT NULL UNIQUE CHECK (length(display_name) BETWEEN 3 AND 40),
    target_profession smallint NOT NULL UNIQUE CHECK (target_profession IN (11,21,41,132,142)),
    version integer NOT NULL CHECK (version > 0),
    enabled boolean NOT NULL DEFAULT true
);

CREATE TABLE gameplay_reference.class_loadout_items (
    loadout_key text NOT NULL REFERENCES gameplay_reference.class_loadouts(loadout_key) ON DELETE CASCADE,
    ordinal smallint NOT NULL CHECK (ordinal BETWEEN 1 AND 12),
    item_type bigint NOT NULL,
    equipment_slot smallint NOT NULL CHECK (equipment_slot IN (1,2,3,4,5,6,8)),
    plus smallint NOT NULL CHECK (plus = 12),
    bless smallint NOT NULL CHECK (bless = 7),
    enchant smallint NOT NULL CHECK (enchant BETWEEN 0 AND 255),
    socket_one smallint NOT NULL CHECK (socket_one IN (3,13,73)),
    socket_two smallint NOT NULL CHECK (socket_two IN (3,13,73)),
    color smallint NOT NULL CHECK (color BETWEEN 0 AND 9),
    bound boolean NOT NULL CHECK (bound),
    PRIMARY KEY (loadout_key,ordinal),
    UNIQUE (loadout_key,equipment_slot,ordinal)
);

INSERT INTO gameplay_reference.class_loadouts(loadout_key,display_name,target_profession,version) VALUES
    ('trojan','Trojan',11,1),('warrior','Warrior',21,1),('archer','Archer',41,1),
    ('water-taoist','Water Taoist',132,1),('fire-taoist','Fire Taoist',142,1);

WITH rows(loadout_key,ordinal,item_type,equipment_slot,gem) AS (VALUES
    ('trojan',1,118109::bigint,1,73),('trojan',2,120249,2,73),('trojan',3,130109,3,73),
    ('trojan',4,150249,6,13),('trojan',5,160249,8,73),('trojan',6,410339,4,13),('trojan',7,420339,5,13),
    ('warrior',1,111109,1,73),('warrior',2,120249,2,73),('warrior',3,131109,3,73),
    ('warrior',4,150249,6,13),('warrior',5,160249,8,73),('warrior',6,480339,4,13),('warrior',7,900109,5,73),
    ('archer',1,113109,1,73),('archer',2,120249,2,73),('archer',3,133109,3,73),
    ('archer',4,150249,6,13),('archer',5,160249,8,73),('archer',6,500329,4,13),
    ('water-taoist',1,114109,1,73),('water-taoist',2,121249,2,73),('water-taoist',3,134109,3,73),
    ('water-taoist',4,152259,6,3),('water-taoist',5,160249,8,73),('water-taoist',6,421339,4,3),
    ('fire-taoist',1,114109,1,73),('fire-taoist',2,121249,2,73),('fire-taoist',3,134109,3,73),
    ('fire-taoist',4,152259,6,3),('fire-taoist',5,160249,8,73),('fire-taoist',6,421339,4,3)
)
INSERT INTO gameplay_reference.class_loadout_items
    (loadout_key,ordinal,item_type,equipment_slot,plus,bless,enchant,socket_one,socket_two,color,bound)
SELECT rows.loadout_key,rows.ordinal,rows.item_type,rows.equipment_slot,
       12,7,255,rows.gem,rows.gem,3,true
FROM rows;

DO $$ BEGIN
    IF (SELECT count(*) FROM gameplay_reference.class_loadout_items) <> 32 THEN
        RAISE EXCEPTION 'reviewed class loadout row count mismatch';
    END IF;
END $$;

CREATE TABLE game.progression_service_receipts (
    operation_id uuid PRIMARY KEY,
    account_id uuid NOT NULL,
    character_id uuid NOT NULL,
    service_key text NOT NULL REFERENCES gameplay_reference.progression_service_definitions(service_key),
    target_profession smallint NULL CHECK (target_profession IS NULL OR target_profession IN (11,21,41,132,142)),
    result_category text NOT NULL CHECK (result_category ~ '^[a-z][a-z0-9-]{2,63}$'),
    charged_conquer_points bigint NOT NULL CHECK (charged_conquer_points IN (0,1000000)),
    created_item_count smallint NOT NULL DEFAULT 0 CHECK (created_item_count BETWEEN 0 AND 12),
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    FOREIGN KEY (character_id,account_id) REFERENCES game.characters(character_id,account_id)
);

CREATE TABLE operations.progression_service_audit (
    audit_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    operation_id uuid NOT NULL,
    account_id uuid NOT NULL,
    character_id uuid NOT NULL,
    legacy_character_uid integer NOT NULL CHECK (legacy_character_uid > 0),
    event_type text NOT NULL CHECK (event_type IN ('service-purchase','gm-cap')),
    safe_category text NOT NULL CHECK (safe_category ~ '^[a-z][a-z0-9-]{2,63}$'),
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    UNIQUE(operation_id,event_type),
    FOREIGN KEY (character_id,account_id) REFERENCES game.characters(character_id,account_id),
    FOREIGN KEY (character_id,legacy_character_uid) REFERENCES game.characters(character_id,legacy_uid)
);

CREATE TRIGGER progression_service_audit_append_only
BEFORE UPDATE OR DELETE ON operations.progression_service_audit
FOR EACH ROW EXECUTE FUNCTION operations.reject_audit_mutation();

INSERT INTO gameplay.feature_gates(feature_key,gate_state,required_migration,required_release_id,changed_by)
VALUES ('progression.services','validated',33,NULL,'migration-v033');
INSERT INTO gameplay.feature_rollouts(feature_key,rollout_scope,changed_by)
VALUES ('progression.services','canary','migration-v033');

GRANT SELECT ON gameplay_reference.progression_service_definitions,
    gameplay_reference.class_loadouts,gameplay_reference.class_loadout_items TO revora_game,revora_backup;
GRANT SELECT,INSERT ON game.progression_service_receipts TO revora_game;
GRANT SELECT ON game.progression_service_receipts TO revora_backup;
GRANT SELECT,INSERT ON operations.progression_service_audit TO revora_game;
GRANT SELECT ON operations.progression_service_audit TO revora_backup;
GRANT USAGE,SELECT ON SEQUENCE operations.progression_service_audit_audit_id_seq TO revora_game,revora_backup;

COMMIT;
