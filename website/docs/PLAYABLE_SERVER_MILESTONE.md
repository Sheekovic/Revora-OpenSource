# Durable server milestone record

Date: 2026-08-28 UTC.

## Operational results

- PostgreSQL 17.11 is independent from AMP, local-only, and migrated to version 3 across `identity`, `game`, `operations`, and `admin` schemas.
- Least-privilege service roles use peer authentication. No database password appears in AMP, process arguments, logs, or Git.
- A custom-format backup was restored into a separate validation database. Migration, realm, and table counts matched before that validation database was removed.
- Revora.Admin runs as `revora-admin` on `127.0.0.1:5081` behind TLS at `https://admin.playrevora.com`. It has separate identities and keys, no public health route, permanent noindex, no registration, forced first-login password replacement, and mandatory TOTP enrollment.
- `PostgreSqlSyntheticTest` validates fragmentation, malformed lengths, generic login failure, random digest-only single-use handoff, wrong-realm/replay rejection, character creation, map state, authoritative movement, local chat, reconnect persistence, two-client visibility, abrupt cleanup, and lifecycle markers.

## Evidence boundary

The synthetic protocol is independently designed test infrastructure. It is not Conquer 5165 and is unavailable in Production. No original 5165 archive was found under `/root`; exact login encryption, packet layouts, game-key transition, and genuine client interoperability remain unverified and unimplemented.

## AMP result

`RevoraDevelopment01` remains stopped. Direct execution as unprivileged `amp` reached READY and its safe console commands worked against PostgreSQL. AMP 2.8.0.4 rootless-container starts were rejected because AMP forced its management and SFTP listeners to `0.0.0.0:8081` and `0.0.0.0:2224`. The instance was stopped after each observation; no game listener or firewall rule was opened. Website, Admin, panel, and legacy AMP access remained healthy.

Narrow backups are outside Git at `/root/postgresql-revora-backups/20260828T140200Z`, `/root/amp-revora-backups/20260828T140600Z`, `/srv/revora-admin/backups/prelaunch-v4-app`, and `/root/nginx-cutover-backups/20260828T133300Z-revora-admin`.

## Next gates

1. Use authenticated ADS to make instance management/SFTP loopback-only, or perform a reviewed stopped-ADS native conversion.
2. Complete two safe AMP lifecycle cycles and one failure test.
3. Obtain, preserve, and hash a lawful original 5165 client, then approve static analysis and deterministic capture.
4. Implement only packet facts corroborated by genuine evidence.

Host lifecycle count note: the suite has ten always-on cases plus one published-executable SIGTERM/redaction case enabled by `REVORA_PUBLISHED_HOST`. Reports of 10 versus 11 reflected the missing environment variable, not deleted coverage. Final validation supplies the artifact and runs all eleven.
