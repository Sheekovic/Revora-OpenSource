# Production operating policy

Public registration and login are continuously available through HTTPS at `playrevora.com`. nginx is the only public HTTP/TLS listener; Web and PostgreSQL remain loopback/Unix-socket only. Registration is not disabled after successful validation.

`revora-web.service` runs as the unprivileged `revora-web` identity with the existing systemd sandbox. Its database login inherits only `revora_web`. HMAC and database configuration are root-owned, group-readable service configuration outside Git; data-protection keys remain in the private writable data tree.

Before a migration, create a protected custom PostgreSQL backup and global-role backup, hash it, restore it into an isolated database, and compare relevant counts. Apply versioned migrations through `revora-migrate`/`revora_migrator`, verify checksum ledger and constraints, then deploy Web and Admin with narrow artifact backups. Validate nginx before any reload. Rollback restores only changed application/config files; additive identity tables may remain safely dormant, while a migration rollback requires a reviewed restore decision.

Rate limits and registration/recovery enable flags are circuit breakers. Operators may disable a flow during abuse without deleting accounts. PostgreSQL, Admin internals, management services, and game secrets are never made public as part of Web deployment.
