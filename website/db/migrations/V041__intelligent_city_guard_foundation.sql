BEGIN;
SET LOCAL ROLE revora_migrator;

CREATE SCHEMA IF NOT EXISTS city_guard;

CREATE TABLE city_guard.character_wanted (
    character_id uuid PRIMARY KEY REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    heat smallint NOT NULL DEFAULT 0 CHECK (heat BETWEEN 0 AND 5),
    last_qualifying_crime_at timestamptz,
    hidden_since timestamptz,
    last_observed_at timestamptz,
    last_observed_map_id integer CHECK (last_observed_map_id IS NULL OR last_observed_map_id > 0),
    timer_frozen boolean NOT NULL DEFAULT false,
    row_version bigint NOT NULL DEFAULT 1,
    CHECK ((heat=0) OR (last_qualifying_crime_at IS NOT NULL AND hidden_since IS NOT NULL))
);

CREATE TABLE city_guard.bounty_cycles (
    bounty_cycle_id uuid PRIMARY KEY,
    wanted_character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    wanted_account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE RESTRICT,
    started_at timestamptz NOT NULL,
    ended_at timestamptz,
    end_reason text CHECK (end_reason IS NULL OR end_reason IN ('pk-below-black','eligible-capture','administrative-forward-fix')),
    claimed_at timestamptz,
    claimed_by_account_id uuid REFERENCES identity.game_accounts(account_id) ON DELETE RESTRICT,
    claimed_by_character_id uuid REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    UNIQUE (wanted_character_id, started_at),
    CHECK (ended_at IS NULL OR ended_at >= started_at),
    CHECK ((claimed_at IS NULL) = (claimed_by_account_id IS NULL))
);
CREATE UNIQUE INDEX bounty_cycles_one_active_uq ON city_guard.bounty_cycles(wanted_character_id) WHERE ended_at IS NULL;

CREATE TABLE city_guard.account_honor_balances (
    account_id uuid PRIMARY KEY REFERENCES identity.game_accounts(account_id) ON DELETE RESTRICT,
    balance integer NOT NULL DEFAULT 0 CHECK (balance >= 0),
    row_version bigint NOT NULL DEFAULT 1,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE city_guard.honor_ledger (
    transaction_id uuid PRIMARY KEY,
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE RESTRICT,
    character_id uuid REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    balance_delta integer NOT NULL CHECK (balance_delta <> 0),
    balance_after integer NOT NULL CHECK (balance_after >= 0),
    reason_type text NOT NULL CHECK (reason_type IN ('bounty-award','reward-redemption','gm-adjustment')),
    bounty_cycle_id uuid REFERENCES city_guard.bounty_cycles(bounty_cycle_id) ON DELETE RESTRICT,
    wanted_account_id uuid REFERENCES identity.game_accounts(account_id) ON DELETE RESTRICT,
    wanted_character_id uuid REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    killer_account_id uuid REFERENCES identity.game_accounts(account_id) ON DELETE RESTRICT,
    killer_character_id uuid REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    pk_points_at_death integer CHECK (pk_points_at_death IS NULL OR pk_points_at_death >= 0),
    map_id integer CHECK (map_id IS NULL OR map_id > 0),
    instance_id integer CHECK (instance_id IS NULL OR instance_id >= 0),
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    eligibility_decision text NOT NULL CHECK (eligibility_decision IN ('award','reject','hold','not-applicable')),
    decision_reason text NOT NULL CHECK (length(decision_reason) BETWEEN 1 AND 200),
    correlation_key text NOT NULL CHECK (length(correlation_key) BETWEEN 16 AND 200),
    audit_metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    UNIQUE(correlation_key)
);
CREATE INDEX honor_ledger_account_time_ix ON city_guard.honor_ledger(account_id,occurred_at DESC);
CREATE INDEX honor_ledger_pair_time_ix ON city_guard.honor_ledger(killer_account_id,wanted_account_id,occurred_at DESC)
    WHERE reason_type='bounty-award' AND eligibility_decision='award';

CREATE TABLE city_guard.bounty_decisions (
    decision_id uuid PRIMARY KEY,
    bounty_cycle_id uuid NOT NULL REFERENCES city_guard.bounty_cycles(bounty_cycle_id) ON DELETE RESTRICT,
    killer_account_id uuid REFERENCES identity.game_accounts(account_id) ON DELETE RESTRICT,
    killer_character_id uuid REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    decision text NOT NULL CHECK (decision IN ('award','reject','hold')),
    reason text NOT NULL CHECK (length(reason) BETWEEN 1 AND 200),
    player_damage_share numeric(6,5) CHECK (player_damage_share BETWEEN 0 AND 1),
    evidence jsonb NOT NULL DEFAULT '{}'::jsonb,
    correlation_key text NOT NULL UNIQUE,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE city_guard.reward_catalog (
    reward_id uuid PRIMARY KEY,
    reward_key text NOT NULL UNIQUE CHECK (reward_key ~ '^[a-z0-9.-]{3,80}$'),
    display_name text NOT NULL CHECK (length(display_name) BETWEEN 1 AND 80),
    honor_cost integer NOT NULL CHECK (honor_cost > 0),
    grant_kind text NOT NULL CHECK (grant_kind IN ('verified-item','status')),
    grant_reference text NOT NULL,
    enabled boolean NOT NULL DEFAULT false,
    evidence_reference text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE TABLE city_guard.reward_redemptions (
    redemption_id uuid PRIMARY KEY,
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE RESTRICT,
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    reward_id uuid NOT NULL REFERENCES city_guard.reward_catalog(reward_id) ON DELETE RESTRICT,
    honor_cost integer NOT NULL CHECK (honor_cost > 0),
    correlation_key text NOT NULL UNIQUE,
    status text NOT NULL CHECK (status IN ('committed','compensated')),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE city_guard.arena_sentences (
    sentence_id uuid PRIMARY KEY,
    character_id uuid NOT NULL REFERENCES game.characters(character_id) ON DELETE RESTRICT,
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE RESTRICT,
    arena_map_id integer NOT NULL DEFAULT 1004 CHECK (arena_map_id=1004),
    instance_id integer NOT NULL UNIQUE CHECK (instance_id > 0),
    sentenced_at timestamptz NOT NULL,
    defeats integer NOT NULL DEFAULT 0 CHECK (defeats BETWEEN 0 AND 3),
    elite_maximum_health bigint NOT NULL CHECK (elite_maximum_health > 0),
    elite_current_health bigint NOT NULL CHECK (elite_current_health BETWEEN 0 AND elite_maximum_health),
    tribute_amount bigint NOT NULL CHECK (tribute_amount BETWEEN 10000 AND 1000000),
    released_at timestamptz,
    release_reason text CHECK (release_reason IS NULL OR release_reason IN ('elite-defeated','tribute-paid','fallback-defeats','fallback-timeout')),
    row_version bigint NOT NULL DEFAULT 1,
    CHECK ((released_at IS NULL) = (release_reason IS NULL))
);
CREATE UNIQUE INDEX arena_sentences_one_active_uq ON city_guard.arena_sentences(character_id) WHERE released_at IS NULL;

CREATE TABLE city_guard.arena_events (
    event_id uuid PRIMARY KEY, sentence_id uuid NOT NULL REFERENCES city_guard.arena_sentences(sentence_id) ON DELETE RESTRICT,
    event_type text NOT NULL CHECK (event_type IN ('sentenced','defeat','disconnect','reconnect','payment','release','elite-damage')),
    correlation_key text NOT NULL UNIQUE, metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE city_guard.achievement_progress (
    account_id uuid NOT NULL REFERENCES identity.game_accounts(account_id) ON DELETE RESTRICT,
    achievement_key text NOT NULL CHECK (achievement_key IN ('pursuit-escape','elite-guard','black-capture','bounty-hunter','city-protector')),
    progress integer NOT NULL DEFAULT 0 CHECK (progress >= 0),
    row_version bigint NOT NULL DEFAULT 1,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY(account_id,achievement_key)
);

CREATE TABLE city_guard.jurisdictions (
    jurisdiction_id uuid PRIMARY KEY, jurisdiction_key text NOT NULL UNIQUE,
    map_id integer NOT NULL CHECK (map_id > 0), instance_id integer NOT NULL DEFAULT 0 CHECK(instance_id >= 0),
    boundary jsonb NOT NULL, gate_pursuit_tiles integer NOT NULL DEFAULT 12 CHECK(gate_pursuit_tiles BETWEEN 0 AND 32),
    enabled boolean NOT NULL DEFAULT false
);
CREATE TABLE city_guard.guard_power_tiers (
    tier_key text PRIMARY KEY, level smallint NOT NULL CHECK(level BETWEEN 1 AND 255), rebirths smallint NOT NULL CHECK(rebirths BETWEEN 0 AND 2),
    maximum_health integer NOT NULL CHECK(maximum_health > 0), minimum_attack integer NOT NULL CHECK(minimum_attack > 0),
    maximum_attack integer NOT NULL CHECK(maximum_attack >= minimum_attack), defense integer NOT NULL CHECK(defense >= 0),
    magic_defense integer NOT NULL CHECK(magic_defense >= 0), accuracy integer NOT NULL CHECK(accuracy >= 0),
    dodge integer NOT NULL CHECK(dodge >= 0), perception integer NOT NULL CHECK(perception BETWEEN 1 AND 32),
    leash integer NOT NULL CHECK(leash BETWEEN 1 AND 64), respawn_seconds integer NOT NULL CHECK(respawn_seconds BETWEEN 5 AND 3600),
    recovery_seconds integer NOT NULL CHECK(recovery_seconds BETWEEN 1 AND 600), configuration jsonb NOT NULL
);
CREATE TABLE city_guard.guard_definitions (
    guard_id uuid PRIMARY KEY, stable_uid integer UNIQUE CHECK(stable_uid IS NULL OR stable_uid > 0),
    guard_key text NOT NULL UNIQUE, role text NOT NULL CHECK(role IN ('warrior','trojan','archer','fire-taoist','water-taoist','ninja','commander','elite')),
    tier_key text NOT NULL REFERENCES city_guard.guard_power_tiers(tier_key), jurisdiction_id uuid REFERENCES city_guard.jurisdictions(jurisdiction_id),
    map_id integer NOT NULL CHECK(map_id > 0), x integer NOT NULL CHECK(x BETWEEN 0 AND 65535), y integer NOT NULL CHECK(y BETWEEN 0 AND 65535),
    direction smallint NOT NULL CHECK(direction BETWEEN 0 AND 7), service_guard boolean NOT NULL DEFAULT false,
    attackable boolean NOT NULL DEFAULT true, enabled boolean NOT NULL DEFAULT false, evidence_reference text NOT NULL,
    CHECK (NOT service_guard OR NOT attackable)
);
CREATE TABLE city_guard.patrol_waypoints (
    guard_id uuid NOT NULL REFERENCES city_guard.guard_definitions(guard_id) ON DELETE RESTRICT,
    ordinal smallint NOT NULL CHECK(ordinal BETWEEN 0 AND 100), x integer NOT NULL CHECK(x BETWEEN 0 AND 65535),
    y integer NOT NULL CHECK(y BETWEEN 0 AND 65535), dwell_ms integer NOT NULL CHECK(dwell_ms BETWEEN 0 AND 60000),
    PRIMARY KEY(guard_id,ordinal)
);

CREATE TABLE city_guard.audit_events (
    event_id uuid PRIMARY KEY, event_type text NOT NULL, account_id uuid, character_id uuid,
    correlation_key text NOT NULL UNIQUE, map_id integer, instance_id integer,
    metadata jsonb NOT NULL DEFAULT '{}'::jsonb, occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE INDEX wanted_active_ix ON city_guard.character_wanted(heat,last_observed_at DESC) WHERE heat>0;
CREATE INDEX arena_active_ix ON city_guard.arena_sentences(sentenced_at) WHERE released_at IS NULL;
CREATE INDEX guard_audit_time_ix ON city_guard.audit_events(event_type,occurred_at DESC);

INSERT INTO gameplay.feature_gates(feature_key,gate_state,required_migration,required_release_id,changed_by) VALUES
 ('guards.system','disabled',41,NULL,'migration-v041'),('guards.player.rendering','disabled',41,NULL,'migration-v041'),
 ('guards.wanted.heat','disabled',41,NULL,'migration-v041'),('guards.blue.warning','disabled',41,NULL,'migration-v041'),
 ('guards.red.pursuit','disabled',41,NULL,'migration-v041'),('guards.black.announcements','disabled',41,NULL,'migration-v041'),
 ('guards.honor.earning','disabled',41,NULL,'migration-v041'),('guards.honor.redemption','disabled',41,NULL,'migration-v041'),
 ('guards.commander','disabled',41,NULL,'migration-v041'),('guards.arena','disabled',41,NULL,'migration-v041'),
 ('guards.twin.city','disabled',41,NULL,'migration-v041'),('guards.ai','disabled',41,NULL,'migration-v041'),
 ('guards.emergency.disable','disabled',41,NULL,'migration-v041') ON CONFLICT(feature_key) DO NOTHING;
INSERT INTO gameplay.feature_rollouts(feature_key,rollout_scope,changed_by)
SELECT feature_key,'disabled','migration-v041' FROM gameplay.feature_gates WHERE feature_key LIKE 'guards.%'
ON CONFLICT(feature_key) DO NOTHING;
INSERT INTO gameplay.feature_gate_dependencies(feature_key,depends_on_feature_key) VALUES
 ('guards.player.rendering','guards.system'),('guards.wanted.heat','guards.system'),('guards.blue.warning','guards.wanted.heat'),
 ('guards.red.pursuit','guards.system'),('guards.black.announcements','guards.red.pursuit'),('guards.honor.earning','guards.black.announcements'),
 ('guards.honor.redemption','guards.commander'),('guards.commander','guards.player.rendering'),('guards.arena','guards.system'),
 ('guards.twin.city','guards.system'),('guards.ai','guards.player.rendering') ON CONFLICT DO NOTHING;

-- Coordinates and player-like presentation remain disabled until the approved
-- DMap tile and genuine 5165 non-account player representation are proven.
INSERT INTO city_guard.guard_power_tiers VALUES
 ('city-veteran',130,1,120000,3500,5000,3000,2500,350,80,18,30,30,10,
  '{"stamina":100,"mana":5000,"cooldown_floor_ms":800,"engagement_cap":4}'::jsonb),
 ('black-elite',130,2,240000,6000,8500,5000,4500,500,100,24,40,45,10,
 '{"stamina":100,"mana":10000,"cooldown_floor_ms":700,"engagement_cap":6}'::jsonb);

INSERT INTO city_guard.jurisdictions(jurisdiction_id,jurisdiction_key,map_id,instance_id,boundary,enabled) VALUES
 ('51650041-0000-4000-8000-000000000001','twin-city-initial',1002,0,
  '{"kind":"polygon","points":[[350,300],[560,300],[560,500],[350,500]],"status":"disabled-pending-live-boundary-validation"}'::jsonb,false);
INSERT INTO city_guard.guard_definitions(guard_id,stable_uid,guard_key,role,tier_key,jurisdiction_id,map_id,x,y,direction,service_guard,attackable,enabled,evidence_reference) VALUES
 ('51650041-0000-4000-8000-000000000002',NULL,'twin-city-commander','commander','city-veteran',
  '51650041-0000-4000-8000-000000000001',1002,455,378,0,true,false,false,
  'V041: requested 456,378 walkable but occupied by production NPC 87468; 455,378 reviewed DMap-walkable and unoccupied within radius 2');

GRANT USAGE ON SCHEMA city_guard TO revora_game,revora_admin,revora_backup;
GRANT SELECT,INSERT,UPDATE ON city_guard.character_wanted,city_guard.bounty_cycles,
 city_guard.account_honor_balances,city_guard.honor_ledger,city_guard.bounty_decisions,
 city_guard.reward_redemptions,city_guard.arena_sentences,city_guard.arena_events,
 city_guard.achievement_progress,city_guard.audit_events TO revora_game;
GRANT SELECT ON ALL TABLES IN SCHEMA city_guard TO revora_admin,revora_backup;

COMMIT;
