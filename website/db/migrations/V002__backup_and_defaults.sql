BEGIN;
SET LOCAL ROLE revora_migrator;

GRANT USAGE ON SCHEMA identity, game, operations, admin TO revora_backup;
GRANT SELECT ON ALL TABLES IN SCHEMA identity, game, operations, admin TO revora_backup;
GRANT SELECT ON ALL SEQUENCES IN SCHEMA identity, game, operations, admin TO revora_backup;

ALTER DEFAULT PRIVILEGES FOR ROLE revora_migrator IN SCHEMA identity, game, operations, admin
    GRANT SELECT ON TABLES TO revora_backup;
ALTER DEFAULT PRIVILEGES FOR ROLE revora_migrator IN SCHEMA operations
    GRANT USAGE, SELECT ON SEQUENCES TO revora_backup;

INSERT INTO operations.backup_status(backup_name, status, safe_detail)
VALUES ('postgresql', 'never', 'No successful backup has been recorded yet.')
ON CONFLICT (backup_name) DO NOTHING;

COMMIT;
