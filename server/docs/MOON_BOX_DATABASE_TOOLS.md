# Moon Box isolated database tools

Prepared 2026-09-21. This resolves tool availability only, not database isolation
or game/client acceptance.

The existing running PostgreSQL executable reports file version 17.11. Its
reduced bundle lacks the provisioning and logical backup tools. A separate
Windows x86-64 archive was obtained through the official
[EDB binary download page](https://www.enterprisedb.com/download-postgresql-binaries),
linked from [PostgreSQL Windows downloads](https://www.postgresql.org/download/windows/).
The selected EDB file ID was `1260569`, redirecting to:

`https://get.enterprisedb.com/postgresql/postgresql-17.11-4-windows-x64-binaries.zip`

Local archive: `C:/Revora/DisposablePostgres/tools-17.11-4/postgresql-17.11-4-windows-x64-binaries.zip`.
Size: 379,726,839 bytes. Observed SHA-256:
`B9424EE7BC60B52450FF910A3630225DF32E633F3CB29C1D126D9299D59AEA28`.
This is a locally measured provenance hash, not a separately verified publisher
checksum or signature.

Only `pgsql/bin`, `pgsql/lib` and `pgsql/share` were extracted: 1,563 files,
141,033,226 uncompressed bytes. Extraction checked every normalized destination
against the new tool directory and refused existing-file replacement. No installer
ran and the live PostgreSQL bundle was not changed. Archive and binaries remain
outside Git; retain this one copy for the upcoming candidate setup.

Executable hashes recorded before their first `--version` execution:

| Tool | Observed SHA-256 | Execution result |
| --- | --- | --- |
| `pg_dump.exe` | `B3A92C4138D453989FBA75D98BB5448685FDDC018E1B1C47339B864FEE765407` | PostgreSQL 17.11, exit 0 |
| `pg_restore.exe` | `95D4165D034CDE580C4514036A2305267043F34B801F0B4C9C2656CA242371EB` | PostgreSQL 17.11, exit 0 |
| `initdb.exe` | `17A208D1F8C5671E8EAF74FC80E3300326CBE15862DBEFBC7490C882BA395FE0` | PostgreSQL 17.11, exit 0 |

## Isolated cluster provisioned

`tools/Initialize-MoonBoxDatabase.ps1` now initializes the fixed candidate at
`C:/Revora/DisposablePostgres/moon-box-candidate/data`. It refuses an existing
candidate directory and verifies the reviewed initdb hash. The directory ACL
allows the current Windows user; a generated bootstrap password is stored using
Windows DPAPI encryption. A private named pipe supplies initdb's password input;
no plaintext password file or command-line password is used. Every generated HBA
connection rule uses SCRAM. Settings specify only IPv4 loopback, port 25439,
20 connections, 32 MB shared buffers and disabled statement logging.

`tools/Verify-MoonBoxDatabase.ps1` verifies the server/control/client hashes and
refuses an existing PID file or occupied candidate port. Two actual start/stop
cycles passed. Each rejected a wrong password, authenticated with the generated
credential and queried the exact candidate data directory, server address and
port. The listener check found only `127.0.0.1:25439`. After final shutdown there
was no candidate PID file or listener. No game server was started.

Additional observed executable hashes used by this check:

| Tool | SHA-256 |
| --- | --- |
| `postgres.exe` | `F1B4E561FF3C6BE6FC7DB26B916BA756508AA0E02C4B4047E5B2DA9CA8C77396` |
| `pg_ctl.exe` | `EED85E36AABC7CDF99A63BDA0A2E33FA3BCE8D3C59D6BC12E9E19469706CC012` |
| `psql.exe` | `E2B971FFE755010DA9F9A3699F62A5F3A5D6000B054B5D80D0E973245F44DEDE` |

Windows validation corrected two setup issues: interactive password prompts did
not consume redirected input, and a PowerShell native-command pipeline waited
on inherited server handles after pg_ctl exited. The scripts now use the private
pipe and explicit pg_ctl process waits. The timed-out initializer was stopped;
its directory, containing only the encrypted password record, is preserved at
`C:/Revora/DisposablePostgres/moon-box-failed-prompt`.

## Game schema and catalogs staged

`tools/Prepare-MoonBoxSchema.ps1` compiled and ran the scoped export helper.
It reads the existing game configuration in memory, requires a literal loopback
source address distinct from candidate port 25439, verifies pg_dump's hash,
and refuses existing output files. Metadata queries run in a read-only
transaction; pg_dump receives read-only, statement-timeout and lock-timeout
settings through its child environment. Passwords are not command-line arguments
or printed. Raw database errors are suppressed. No restore was performed.

Local artifacts under the protected candidate root:

| Artifact | Evidence |
| --- | --- |
| `source-schema-columns.tsv` | Names/types only for the public schema's 100 base tables; no row values. |
| `game-schema.dump` | Schema only, 100,666 bytes; SHA-256 `2C8C7BD52A173FB52B9150D77CC7272CEA012E847283183C5B4B828A879F5967`. The restore inventory contains no TABLE DATA, functions, procedures, triggers or foreign tables. |
| `game-catalogs.dump` | Explicit allowlist, 95,332 bytes; SHA-256 `4C58A4D1F7D24F275E220D93062F6ECDC670CFADAABDAF24FC670FB6D0900BEE`. Restore inventory verifies exactly the fifteen table-data entries below and three catalog sequence values. |

The `-Catalogs` mode exports `configuration`, `maps`, `monsterinfos`,
`monsterspawns`, `npcs`, `sobnpcs`, `spells`, `refinery`, `refineryboxes`,
`databaserefinery`, `databaserefineryboxes`, `titles`, `lottery`, `lotteryslayer`
and `premioeventos`. This is an initial catalog set, not proof that every startup
dependency is satisfied. Configuration counters/rates require candidate review.
The three sequence values belong to monster spawns, SOB NPCs and spells.

Accounts, entities, items, skills, payments and website table rows are excluded.
Column inspection also excluded deceptively named state tables: `furniture`
contains owner/map placement, `itemadding` contains item ownership/upgrades,
`status` contains entity state, and `prizenpc` contains owned pending rewards.
All dumps and catalog content remain outside Git.

## Catalog restore and restart verified

The reviewed archives are now restored into `revora_candidate_game` on the
isolated cluster. Its owner role has the same name and has no login, superuser,
database-creation, role-creation or replication privilege. Bootstrap credentials
remain separate; a restricted runtime login still needs provisioning before game
startup. All 100 public game tables belong to the candidate owner.

`tools/Verify-MoonBoxDatabase.ps1 -RestoreCatalogs` verifies the exact archive
hashes before startup, checks the candidate cluster identity, and creates only
the fixed candidate role/database. Schema and catalog restores each use a single
transaction, stop on errors and set the candidate owner role. They do not restore
source ownership or ACLs. This is not one combined transaction across both files;
a catalog failure can leave a schema-only candidate requiring inspection.

The first schema restore rolled back because its `CREATE SCHEMA public`
conflicted with PostgreSQL's empty default schema. The corrected script removes
only the candidate's empty public schema without CASCADE. An explicit
`-ResumeEmptyRestore` recovery verifies candidate ownership, no public relations
and no additional user schemas before that removal. It cannot replace a populated
candidate. Recovery passed, followed by both complete start/stop cycles.

Before and after the actual database restart, validation confirmed 100 owned
tables, the Twin City map, nonempty monster/NPC/configuration catalogs, empty
accounts/entities/items/skills tables and the non-login, non-superuser owner.
Authentication rejection, exact data-directory/address/port and loopback-only
listener checks also passed. A deliberate retry with empty-restore recovery
against this populated database was refused and left the cluster stopped.
`-VerifyCatalogs` can repeat the read-only catalog checks when later changes
justify validation; it performs no restore.

## Runtime database login provisioned

`tools/Verify-MoonBoxDatabase.ps1 -VerifyCatalogs -ProvisionRuntime` created
`revora_candidate_runtime` in the isolated cluster. It inherits the candidate
game owner role, can log in with SCRAM and has no superuser, role-creation,
database-creation or replication privilege. Its connection limit is 20.
The original owner role remains non-login and the bootstrap role stays separate.

The generated runtime credential is stored under the protected candidate root
as `runtime-password.dpapi.xml`, encrypted with Windows DPAPI for the current
user. `Invoke-CandidateRuntimeCheck.ps1` supplies credentials only to its helper's
process environment. The helper removes those environment entries after reading
them; raw database errors are not printed. No plaintext INI/password file was
created. Password assignment binds a transaction-local setting and uses a fixed
server-side block with PostgreSQL `%L` literal quoting; ALTER ROLE itself cannot
take a password bind parameter. PostgreSQL stores the resulting SCRAM credential.

Before and after the actual database restart, the runtime helper authenticated,
checked its identity and privileges, read catalogs, created/inserted a probe table
and altered the accounts table inside a transaction. All probe changes rolled
back and their absence was verified. A real `SET ROLE revora_candidate_admin`
attempt was denied with SQLSTATE 42501, and a wrong password was rejected with
28P01. The surrounding cluster checks again confirmed 100 owned game tables,
empty player tables, loopback-only binding and clean shutdown.

`-VerifyRuntime` repeats only runtime validation when justified; it does not
recreate or rotate the credential. `-ProvisionRuntime` refuses existing credential
files/roles rather than silently replacing them. The database login is ready for
the candidate game process, but that process has not been configured or started.

## Ordinary candidate account created

`tools/Verify-MoonBoxDatabase.ps1 -VerifyRuntime -ProvisionAccount` created
`MoonBoxTester` through the server's `AccountTable.Insert` using the restricted
candidate runtime database login. The account has ordinary player state 2 and
EntityID 0: no character exists yet. Its password was generated locally, stored
only in DPAPI-encrypted `account-password.dpapi.xml`, and passed to the helper
through a temporary child environment. The database stores the format marker
and salt/verifier, not the supplied password. No credential/proof was printed.

The helper reloads the actual account and checks a client-side SRP proof using
the independent calculation shared with the SRP fixtures. Correct authentication,
wrong-password rejection and consumed-proof rejection passed before and after a
real database restart. These are in-process protocol checks, not native-client
or account/game TCP acceptance. The companion runtime privilege checks also
passed on both sides of that restart.

The candidate now has the 100 base game tables plus `revora_account_srp`, owned
by the runtime role. A subsequent combined `-VerifyCatalogs -VerifyAccount` run
passed before/after restart: one ordinary account, no character/items/skills,
the expected table ownership and catalogs, and the saved credential. Use explicit
`-VerifyAccount` with catalog verification for this prepared state; the earlier
empty-account expectation is no longer applicable. Provisioning refuses an
existing credential file or nonempty accounts table rather than overwriting it.

Next: select separate mutable game files, establish approved-client routing and
create/prepare the eligible test character. The cluster is stopped. No production
state was changed; no Moon Box ordinary-player acceptance gate is satisfied by
these infrastructure checks. No broad gameplay suite was rerun.
