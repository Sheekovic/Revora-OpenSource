BEGIN;
SET LOCAL ROLE revora_migrator;

-- The exact patch-5165 character-registration jump table emits the base
-- profession set 10, 20, 40, 50, and 100. Value 30 is not emitted by that
-- builder; value 50 is the client-supported Water Taoist creation code.
ALTER TABLE game.characters
    DROP CONSTRAINT characters_profession_check,
    ADD CONSTRAINT characters_profession_check
        CHECK (profession IN (10, 20, 40, 50, 100));

COMMIT;
