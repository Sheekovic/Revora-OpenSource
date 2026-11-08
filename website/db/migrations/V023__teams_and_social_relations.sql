BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE TABLE game.teams (
    team_id uuid PRIMARY KEY,
    leader_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version > 0),
    UNIQUE (team_id, leader_character_id)
);

CREATE TABLE game.team_members (
    team_id uuid NOT NULL REFERENCES game.teams(team_id) ON DELETE CASCADE,
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    joined_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY (team_id, character_id),
    UNIQUE (character_id)
);

CREATE TABLE game.social_relations (
    owner_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    other_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    relation_kind text NOT NULL CHECK (relation_kind IN ('friend', 'enemy')),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY (owner_character_id, other_character_id, relation_kind),
    CHECK (owner_character_id <> other_character_id)
);

CREATE TABLE game.social_invitations (
    invitation_id uuid PRIMARY KEY,
    sender_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    recipient_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE CASCADE,
    invitation_kind text NOT NULL CHECK (invitation_kind IN ('team', 'friend')),
    expires_at timestamptz NOT NULL,
    consumed_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CHECK (sender_character_id <> recipient_character_id),
    CHECK (expires_at > created_at)
);

CREATE INDEX social_invitations_recipient_open_idx
    ON game.social_invitations(recipient_character_id, expires_at) WHERE consumed_at IS NULL;

GRANT SELECT, INSERT, UPDATE, DELETE ON game.teams, game.team_members,
    game.social_relations, game.social_invitations TO revora_game;
GRANT SELECT ON game.teams, game.team_members, game.social_relations,
    game.social_invitations TO revora_backup;

COMMIT;
