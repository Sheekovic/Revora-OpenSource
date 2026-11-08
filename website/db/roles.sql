-- Run as the PostgreSQL cluster administrator. This file contains no credentials.
DO $roles$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'revora_migrator') THEN CREATE ROLE revora_migrator NOLOGIN; END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'revora_game') THEN CREATE ROLE revora_game NOLOGIN; END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'revora_admin') THEN CREATE ROLE revora_admin NOLOGIN; END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'revora_web') THEN CREATE ROLE revora_web NOLOGIN; END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'revora_reporting') THEN CREATE ROLE revora_reporting NOLOGIN; END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'revora_backup') THEN CREATE ROLE revora_backup NOLOGIN; END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'revora-migrate') THEN CREATE ROLE "revora-migrate" LOGIN; END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'revora-admin') THEN CREATE ROLE "revora-admin" LOGIN; END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'revora-web') THEN CREATE ROLE "revora-web" LOGIN; END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'revora-backup') THEN CREATE ROLE "revora-backup" LOGIN; END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'amp') THEN CREATE ROLE amp LOGIN; END IF;
END
$roles$;

GRANT revora_migrator TO "revora-migrate";
GRANT revora_game TO amp;
GRANT revora_admin TO "revora-admin";
GRANT revora_web TO "revora-web";
GRANT revora_backup TO "revora-backup";
