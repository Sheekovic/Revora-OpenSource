BEGIN;
SET LOCAL ROLE revora_migrator;

ALTER TABLE game.characters
    DROP CONSTRAINT characters_profession_check;
ALTER TABLE game.characters
    ADD CONSTRAINT characters_profession_check CHECK
        (profession IN (10,11,12,13,14,15,
                        20,21,22,23,24,25,
                        40,41,42,43,44,45,
                        50,51,52,53,54,55,
                        100,132,133,134,135,142,143,144,145));

COMMIT;
