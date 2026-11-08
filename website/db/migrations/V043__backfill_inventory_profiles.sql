BEGIN;
SET LOCAL ROLE revora_migrator;

-- V014 introduced inventory profiles after some characters already existed. The
-- runtime treats a missing profile as the historical 40-slot default when it
-- loads character state, but transactional slot reservation requires a durable
-- profile row. Backfill only missing rows; never alter an existing capacity.
INSERT INTO game.inventory_profiles(character_id, slot_capacity)
SELECT c.character_id, 40
FROM game.characters c
LEFT JOIN game.inventory_profiles p ON p.character_id = c.character_id
WHERE p.character_id IS NULL
ON CONFLICT (character_id) DO NOTHING;

COMMIT;
