BEGIN;
SET LOCAL ROLE revora_migrator;

GRANT DELETE ON identity.network_history, identity.recognized_devices, identity.web_sessions,
    identity.reset_authorizations, identity.pending_recoveries TO revora_web;

COMMIT;
