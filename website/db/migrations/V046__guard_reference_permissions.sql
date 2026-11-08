BEGIN;

-- V041 intentionally left every guard runtime gate disabled, but its bounded
-- persistence store reads these immutable catalogs before any mutation. Grant
-- only the missing read privilege to the game service; no guard gate is enabled.
GRANT SELECT ON city_guard.guard_definitions, city_guard.guard_power_tiers,
 city_guard.jurisdictions, city_guard.patrol_waypoints, city_guard.reward_catalog
 TO revora_game;

-- Equipment-aware 10014 peer presentation is a separately reversible canary.
-- No identity is selected here; deployment must resolve and pin the immutable
-- AccountId/CharacterId pair before inserting a feature_canaries row.
INSERT INTO gameplay.feature_gates(feature_key,gate_state,required_migration,required_release_id,changed_by)
SELECT 'world.player.equipment.visibility','enabled',46,release_id,'migration-v046'
FROM gameplay_reference.catalog_releases WHERE release_state='active'
ON CONFLICT(feature_key) DO NOTHING;
INSERT INTO gameplay.feature_rollouts(feature_key,rollout_scope,changed_by)
VALUES ('world.player.equipment.visibility','canary','migration-v046')
ON CONFLICT(feature_key) DO NOTHING;
INSERT INTO gameplay.feature_gate_dependencies(feature_key,depends_on_feature_key) VALUES
 ('world.player.equipment.visibility','inventory'),
 ('world.player.equipment.visibility','equipment.stats')
ON CONFLICT DO NOTHING;

COMMIT;
