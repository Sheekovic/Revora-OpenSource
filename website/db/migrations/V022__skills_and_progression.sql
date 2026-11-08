BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE gameplay_reference.magic_types (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    magic_type integer NOT NULL CHECK (magic_type BETWEEN 1 AND 65535),
    level smallint NOT NULL CHECK (level BETWEEN 0 AND 255),
    sort smallint NOT NULL CHECK (sort BETWEEN 0 AND 255),
    safe_name text NOT NULL CHECK (length(safe_name) BETWEEN 1 AND 128 AND safe_name !~ '[[:cntrl:]]'),
    mana_cost integer NOT NULL,
    base_damage bigint NOT NULL CHECK (base_damage >= 0),
    activation_chance smallint NOT NULL CHECK (activation_chance BETWEEN 0 AND 100),
    required_level smallint NOT NULL CHECK (required_level BETWEEN 0 AND 255),
    required_experience bigint NOT NULL CHECK (required_experience >= 0),
    canonical_record_sha256 text NOT NULL CHECK (canonical_record_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (source_id, magic_type, level)
);

CREATE TABLE gameplay_reference.character_level_requirements (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    level smallint NOT NULL CHECK (level BETWEEN 1 AND 255),
    required_experience bigint NOT NULL CHECK (required_experience >= 0),
    PRIMARY KEY (source_id, level)
);

CREATE TABLE gameplay_reference.base_character_stats (
    source_id uuid NOT NULL REFERENCES gameplay_reference.import_sources(source_id),
    profession_family text NOT NULL CHECK (profession_family IN ('trojan', 'warrior', 'archer', 'taoist')),
    level smallint NOT NULL CHECK (level BETWEEN 1 AND 255),
    strength integer NOT NULL CHECK (strength BETWEEN 0 AND 65535),
    agility integer NOT NULL CHECK (agility BETWEEN 0 AND 65535),
    vitality integer NOT NULL CHECK (vitality BETWEEN 0 AND 65535),
    spirit integer NOT NULL CHECK (spirit BETWEEN 0 AND 65535),
    PRIMARY KEY (source_id, profession_family, level)
);

CREATE TABLE game.character_skills (
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    magic_source_id uuid NOT NULL,
    magic_type integer NOT NULL,
    level smallint NOT NULL CHECK (level BETWEEN 0 AND 255),
    experience bigint NOT NULL DEFAULT 0 CHECK (experience >= 0),
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version > 0),
    PRIMARY KEY (character_id, magic_type),
    FOREIGN KEY (magic_source_id, magic_type, level)
        REFERENCES gameplay_reference.magic_types(source_id, magic_type, level)
);

CREATE TABLE game.character_proficiencies (
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    proficiency_type integer NOT NULL CHECK (proficiency_type BETWEEN 1 AND 65535),
    level smallint NOT NULL DEFAULT 0 CHECK (level BETWEEN 0 AND 20),
    experience bigint NOT NULL DEFAULT 0 CHECK (experience >= 0),
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version > 0),
    PRIMARY KEY (character_id, proficiency_type)
);

CREATE TABLE game.character_progression (
    character_id uuid PRIMARY KEY REFERENCES game.characters(character_id) ON DELETE CASCADE,
    rebirth_count smallint NOT NULL DEFAULT 0 CHECK (rebirth_count BETWEEN 0 AND 2),
    previous_profession smallint NOT NULL DEFAULT 0 CHECK (previous_profession BETWEEN 0 AND 255),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version > 0)
);

GRANT SELECT ON gameplay_reference.magic_types,
    gameplay_reference.character_level_requirements,
    gameplay_reference.base_character_stats TO revora_game, revora_backup;
GRANT SELECT, INSERT, UPDATE, DELETE ON game.character_skills,
    game.character_proficiencies TO revora_game;
GRANT SELECT, INSERT, UPDATE ON game.character_progression TO revora_game;
GRANT SELECT ON game.character_skills, game.character_proficiencies,
    game.character_progression TO revora_backup;

COMMIT;
