BEGIN;
SET LOCAL ROLE revora_migrator;

GRANT SELECT ON operations.schema_migrations, operations.backup_status TO revora_game;

COMMIT;
