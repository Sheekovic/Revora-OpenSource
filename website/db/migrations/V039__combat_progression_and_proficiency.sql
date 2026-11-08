BEGIN;
SET LOCAL ROLE revora_migrator;

ALTER TABLE game.combat_operation_receipts
    ADD COLUMN character_experience_awarded bigint NOT NULL DEFAULT 0
        CHECK (character_experience_awarded >= 0),
    ADD COLUMN resulting_character_level smallint NULL CHECK (resulting_character_level BETWEEN 1 AND 130),
    ADD COLUMN resulting_character_experience bigint NULL CHECK (resulting_character_experience >= 0),
    ADD CONSTRAINT combat_progression_result_pair CHECK (
        (resulting_character_level IS NULL) = (resulting_character_experience IS NULL));

CREATE TABLE game.combat_proficiency_results (
    operation_id uuid NOT NULL REFERENCES game.combat_operation_receipts(operation_id) ON DELETE RESTRICT,
    proficiency_type integer NOT NULL CHECK (proficiency_type BETWEEN 1 AND 65535),
    experience_awarded bigint NOT NULL CHECK (experience_awarded >= 0),
    resulting_level smallint NOT NULL CHECK (resulting_level BETWEEN 0 AND 20),
    resulting_experience bigint NOT NULL CHECK (resulting_experience BETWEEN 0 AND 4294967295),
    PRIMARY KEY (operation_id,proficiency_type)
);

CREATE TABLE gameplay_reference.proficiency_level_requirements (
    level smallint PRIMARY KEY CHECK (level BETWEEN 0 AND 19),
    required_experience bigint NOT NULL CHECK (required_experience >= 0),
    source_path text NOT NULL,
    source_sha256 text NOT NULL CHECK (source_sha256 ~ '^[0-9a-f]{64}$')
);

INSERT INTO gameplay_reference.proficiency_level_requirements(level,required_experience,source_path,source_sha256)
VALUES
 (0,0,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (1,100,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (2,600,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (3,250,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (4,6400,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (5,1600,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (6,4000,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (7,1000,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (8,22000,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (9,4000,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (10,10000,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (11,95000,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (12,14000,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (13,21000,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (14,32500,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (15,48000,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (16,721250,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (17,108275,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (18,162363,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e'),
 (19,210000,'MySql/Database.cs','2c3e448ff3693b490841ffd99bbb55b58ac104dc909f4b3dfd4aa3b13c75618e');

UPDATE gameplay.feature_gates SET required_migration=39
WHERE feature_key IN ('combat.runtime','ninja.native');

GRANT SELECT, INSERT ON game.combat_proficiency_results TO revora_game;
GRANT SELECT ON game.combat_proficiency_results TO revora_backup;
GRANT SELECT ON gameplay_reference.proficiency_level_requirements TO revora_game,revora_backup;

COMMIT;
