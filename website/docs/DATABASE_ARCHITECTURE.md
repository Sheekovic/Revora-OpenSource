# Database Architecture

Status: PostgreSQL foundation and public Web identity are active.

PostgreSQL 17.11 is installed from Debian and listens only on localhost plus protected Unix sockets. Database `revora` is at migration 8. Runtime, Web, Admin, migration, reporting, and backup roles are separate; applications do not use the superuser. The system data directory remains PostgreSQL-owned outside AMP. A protected custom-format backup was restored into a separate validation database and its schema and representative counts matched before the public-identity migrations were applied.

## Decision

Revora selects PostgreSQL for its future durable store. The decision favors transactional correctness, mature concurrency controls, partial indexes, constraints, JSON support for bounded metadata, reliable point-in-time recovery, and the mature Npgsql .NET driver. MariaDB remains a viable import staging tool when legacy data is naturally expressed in MySQL-compatible dumps, but it is not the authoritative Revora store.

| Criterion | PostgreSQL | MariaDB |
| --- | --- | --- |
| Transactions and concurrency | Strong MVCC, rich constraints and isolation | Mature InnoDB; semantics depend more on engine/configuration |
| .NET integration | Mature Npgsql provider and migration ecosystem | Mature MySqlConnector ecosystem |
| Debian 13 operations | Supported packages, local socket/loopback deployment | Supported packages, local socket/loopback deployment |
| Backup/restore | `pg_dump`/`pg_restore`, WAL/PITR options | `mariadb-dump`, physical backup options |
| Legacy import | Requires explicit mapping/conversion | Often closer to legacy MySQL schemas |
| Long-term model | Best fit for strict constraints and auditability | Good, but offers no decisive Revora advantage |

The reviewed Npgsql provider backs parameterized persistence operations for Account, Game, Web and Admin services.

## Bounded data model

- Authoritative identity: immutable AccountId, normalized unique username, reviewed password hash, status, Player role and security stamp.
- Web security: digest-only sessions, recovery codes, reset authorizations, recognized devices, privacy-preserving familiar-network history, supporting security questions and sanitized pending recoveries. No email column is required for public registration.
- Game identity: the same AccountId and password verifier consumed by genuine Account packet 1086, realm membership and compatibility-adapter metadata.
- Realm/game state: realms, characters and future aggregate-owned game state.
- Content: localized news, stable slugs and publication state.
- Distribution: downloads, immutable patch manifests, channels and signing-key identifiers (public identifiers only).
- Administration: roles, explicit grants and append-only audit records.

Identifiers are opaque UUIDs. Timestamps are UTC. Foreign keys, unique constraints and optimistic concurrency tokens protect invariants. Secrets and password hashes are never written to audit summaries.

## Access and operations

Repositories expose parameterized operations; no route accepts or constructs arbitrary SQL. Production uses a mode `0600` secret file readable only by the AMP instance user. PostgreSQL will bind to loopback or a Unix socket only. Migrations are reviewed artifacts, disabled automatically in production, and applied as an explicit maintenance action with a verified backup and rollback plan.

Backups must be transactionally consistent logical dumps or reviewed physical/PITR backups—not copies of live database files. Development, staging and production use separate roles, databases, secrets and backup scopes.
