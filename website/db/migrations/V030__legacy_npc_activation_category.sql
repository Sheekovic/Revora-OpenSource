BEGIN;
SET LOCAL ROLE revora_migrator;

ALTER TABLE gameplay_reference.npcs
    DROP CONSTRAINT npcs_active_month_check,
    ADD CONSTRAINT npcs_active_month_check CHECK (active_month BETWEEN 0 AND 13);

COMMENT ON COLUMN gameplay_reference.npcs.active_month IS
    'Legacy activation category: 0 is ordinary availability, 1-12 are calendar months, and 13 is the reviewed seasonal-event category loaded explicitly by the owner source.';

COMMIT;
