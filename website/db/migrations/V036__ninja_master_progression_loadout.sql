BEGIN;
SET LOCAL ROLE revora_migrator;

-- Ninja is native to the approved 5165 client. Rebirth remains deliberately
-- restricted to the previously reviewed five classes; this migration expands
-- only the data-driven equipment-package target.
ALTER TABLE gameplay_reference.class_loadouts
    DROP CONSTRAINT class_loadouts_target_profession_check;
ALTER TABLE gameplay_reference.class_loadouts
    ADD CONSTRAINT class_loadouts_target_profession_check CHECK
        (target_profession IN (11,21,41,55,132,142));

ALTER TABLE game.progression_service_receipts
    DROP CONSTRAINT progression_service_receipts_target_profession_check;
ALTER TABLE game.progression_service_receipts
    ADD CONSTRAINT progression_service_receipts_target_profession_check CHECK
        (target_profession IS NULL OR target_profession IN (11,21,41,55,132,142));

INSERT INTO gameplay_reference.class_loadouts
    (loadout_key,display_name,target_profession,version,enabled)
VALUES ('ninja','Ninja',55,1,true);

-- Every ItemType is an exact sparse record activated by V035. Both native
-- Ninja head styles are supplied as inventory choices; only one can occupy
-- equipment slot 1. The two EmperorKatanas become distinct immutable item
-- instances and support the genuine dual-hand flow.
WITH rows(ordinal,item_type,equipment_slot,gem) AS (VALUES
    (1,123109::bigint,1,73), -- ForceHood
    (2,112109::bigint,1,73), -- GrudgeVeil
    (3,120249::bigint,2,73), -- Tornado necklace
    (4,135109::bigint,3,73), -- OwlVest
    (5,601339::bigint,4,13), -- EmperorKatana, right hand
    (6,601339::bigint,5,13), -- EmperorKatana, left hand
    (7,150249::bigint,6,13), -- Thunder ring
    (8,160249::bigint,8,73)  -- Blizzard boots
)
INSERT INTO gameplay_reference.class_loadout_items
    (loadout_key,ordinal,item_type,equipment_slot,plus,bless,enchant,
     socket_one,socket_two,color,bound)
SELECT 'ninja',ordinal,item_type,equipment_slot,12,7,255,gem,gem,3,true
FROM rows;

DO $$
DECLARE
    resolved integer;
BEGIN
    IF (SELECT count(*) FROM gameplay_reference.class_loadout_items
        WHERE loadout_key='ninja') <> 8 THEN
        RAISE EXCEPTION 'reviewed Ninja loadout row count mismatch';
    END IF;

    SELECT count(*) INTO resolved
    FROM gameplay_reference.class_loadout_items i
    WHERE i.loadout_key='ninja'
      AND EXISTS (
          SELECT 1
          FROM gameplay_reference.catalog_releases r
          JOIN gameplay_reference.item_type_release_entries e USING(release_id)
          JOIN gameplay_reference.item_types t
            ON t.source_id=e.source_id AND t.item_type=e.item_type
          WHERE r.release_state='active' AND e.item_type=i.item_type
            AND (t.item_class=50 OR i.item_type IN (120249,150249,160249))
      );
    IF resolved <> 8 THEN
        RAISE EXCEPTION 'reviewed Ninja loadout does not resolve against the active exact-client catalog';
    END IF;
END $$;

COMMIT;
