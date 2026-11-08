# Moon Box native acceptance record

Prepared 2026-09-21. **Native checks not executed; not release approval.** This record separates
existing automated evidence from the ordinary-player observations still needed.

## Identified artifacts

- Approved client: `C:/Users/Administrator/Desktop/Revora/6711/Env_DX9/Conquer.exe`.
  Read-only SHA-256 verification matches the owner-approved DX9 profile:
  `3728BC3FD4E584D85301EDD2B1887A176267CDAA6356038F9951EF23E4FDED60`.
  Required client argument: `blacknull`. This is identity evidence only, not
  proof that its launch configuration reaches an isolated server.
- The top-level `6711/Conquer.exe` has a different SHA-256:
  `9BD5D9C39B3EB531E4C7A03EECDD363A418C272AC11D02DBA382922BF3256F94`.
  Do not substitute it for the approved DX9 executable.
- Existing isolated `obj/chi-refill-validation/bin/Revora.exe` SHA-256:
  `5A673C1977DBDA199BE68F031E1DFABC1CD4EFF5E773678845A95914B892D687`.
  This identifies the observed file, not a reproducible release or a verified
  match to clean Git source. The worktree also contains owner edits.

## Candidate setup still required

`Program.Main` ignores its arguments and invokes `Server.Run` directly.
`Server.Run` reads `Configuracion.ini`; `System/IniFile.cs` resolves it against
the process working directory. The same startup reads database settings and
listener addresses and initializes systems beyond Moon Box. There is no
Moon-Box-only command-line mode. Running the existing binary in the normal
runtime directory is therefore not an isolated acceptance test.

Follow-up source check: both Account and Game sockets bind the configured
`GameIP`, but startup writes database state before reaching either listener
(including character chat/vote flags). A bind failure therefore does not imply
that the database was untouched. An isolated database must exist before the
first candidate launch; changing only the ports is insufficient.

`DataHolder.CreateConnection` now uses `NpgsqlConnectionStringBuilder` so literal
password, username and database values cannot introduce extra connection options.
The existing host/port syntax, default port 25432 and pool settings are retained.
The isolated build and 19 synthetic connection-configuration checks pass via
`tests/Verify-ConnectionConfig.ps1`; these checks open no database connection.
They do not establish that candidate settings point to an isolated runtime.

Local tooling inventory, 2026-09-21: the running PostgreSQL process identifies
`out/Revora-Server/pgsql/bin/postgres.exe` under the parent Revora workspace.
That directory contains `postgres.exe` and `pg_ctl.exe`, but no `pg_dump.exe`,
`pg_restore.exe` or `initdb.exe`. A filename-only search of the parent's `out`,
`artifacts` and `research` trees found none of those three tools. The inspected
`C:/Revora/DisposablePostgres` directory was empty. These bounded observations
do not establish that tools are unavailable elsewhere or cannot be obtained.
The fixture runner prepares selected tables in a disposable schema; it does not
bootstrap the full game database. Candidate setup must supply compatible database
provisioning/restore tools and reviewed catalogs before launch. Local mutable
files also need isolation: examples include `database/InnerPower.txt`,
`database/Slayer/KOBoard.dat` and `database/racerecords.ini`.

The missing-tool prerequisite was subsequently resolved, without touching the
running bundle: PostgreSQL 17.11 tools are now extracted under
`C:/Revora/DisposablePostgres/tools-17.11-4/pgsql`. `pg_dump`, `pg_restore` and
`initdb` each successfully report version 17.11. See the
[tool provenance](MOON_BOX_DATABASE_TOOLS.md). No database was initialized,
copied or connected by those version checks.

The separate cluster has now been initialized at
`C:/Revora/DisposablePostgres/moon-box-candidate/data` and verified through two
start/authentication/identity/stop cycles on `127.0.0.1:25439`. It is stopped.
The separate `revora_candidate_game` database now contains the reviewed schema
and fifteen allowlisted catalog tables. Its 100 base public tables have the
candidate owner; `revora_account_srp` belongs to the runtime role. One ordinary
`MoonBoxTester` account now exists with EntityID 0 and a stored verifier;
entities/items/skills remain empty. Reload and proof checks pass across a database
restart. The owner cannot log in and is not a superuser. A separate
`revora_candidate_runtime` login now inherits that owner role, with no superuser,
database-creation, role-creation or replication privilege. Authentication and
transactional DDL/DML checks passed before and after restart, and bootstrap role
access was denied. The isolated game runtime now passes the listener/normal-close
check described below. An eligible test character and native client connection
remain unprepared.
The database checklist below therefore stays open. See the tool record for
reusable scripts, credential handling and exact validation scope.

Runtime credential prerequisite: `Server.Run` now accepts
`REVORA_DATABASE_PASSWORD` from its process environment, falling back to the
existing INI field only when the variable is absent. A candidate launcher can
decrypt its protected credential in memory and pass it to the child without a
plaintext INI password. This does not provision the runtime login or establish
an authenticated game session. The isolated build and 19 existing connection
configuration checks passed; those checks do not exercise native startup.

Account prerequisite discovered during setup: `AccountTable` loads `accounts.password`
as a string and writes it back from `Save`/`Insert`; `Server` directly compares
that value and passes it to `Srp6711Exchange.Begin`. Creating a new native-test
account through this path would persist a plaintext password, contrary to the
owner's repository instructions. No test account was created. A supported secure
account/verifier path is needed before ordinary-player login acceptance; an auth
bypass or fixture-only session does not satisfy that gate. Existing production
accounts were neither inspected for values nor modified during this review.

The first prerequisite is now implemented in `Srp6711Exchange`: `CreateVerifier`
derives a fixed 32-byte verifier with a random 32-byte salt, and
`BeginWithVerifier` starts a fresh exchange using those saved values. The existing
`Begin` API delegates through this path. An isolated build and 46 independent
synthetic-client checks pass, including correct/wrong credentials, username
binding, replay, cross-exchange rejection, malformed records and caller-buffer
mutation. This does not migrate account storage or enable verifier account
dispatch; no test account was created. It preserves legacy 6711 arithmetic and
does not establish modern cryptographic strength or genuine-client acceptance.

Follow-up integration: server `AccountTable.Insert` and explicit password changes
through `Save` now atomically store a marker plus `revora_account_srp` salt/verifier
instead of the supplied password. Ordinary profile saves do not rewrite passwords.
The loader recognizes the marker, requires a valid credential and clears the
password property. Authentication dispatch uses the stored verifier and still
requires the 1214 proof. New authentication requests discard any previous pending
exchange. Missing, malformed and ambiguous identities fail closed.

The isolated build, 33 account database checks and 46 SRP checks pass. Database
fixtures use only `revora_login_validation`, including ordinary account creation,
reload, profile save, password reset, legacy reset conversion, duplicate identity,
suppressed verifier-write rollback and invalid/missing credential rejection.
These are synthetic accounts; the actual candidate account has not been created.
The server no longer logs auth packet hex/forwarding tokens or writes the old
auth-capture diagnostic; two chat password-display paths now refuse disclosure.
This is a scoped change, not an audit of every legacy logging or admin path.

Existing legacy accounts are not bulk-migrated. External website registration,
password reset and HTTP login compatibility still require coordinated review;
they are not converted by this server-only change. Native login and modern
password-storage security remain separate gates.

Runtime data inventory: `bin/Debug/database` contains 2,272 files totaling
376,645,604 bytes, including catalogs/maps and mutable character/operator state.
Do not copy that whole directory as if it were a clean catalog. Its
`Slayer/KOBoard.dat`, character progression files and log directories require
separate handling; an explicit runtime file selection is still pending.

- [x] Record the candidate source base and intentional local changes in the
  private manifest; prepare its own working directory and data paths. This is
  not a clean-source production release artifact.
- [x] Start the composed candidate with three loopback listeners and close via
  its normal command with exit code zero, then confirm listener cleanup.
- [ ] Establish a disposable database with the required catalogs and an ordinary
  eligible test character. The existing fixture schema is not evidence of a
  complete server runtime. Keep credentials out of this record and Git.
- [ ] Verify candidate listener addresses are loopback and its database/data
  destinations cannot modify production. Review other startup initializers;
  leave the staged joint Arena cutover disabled.
- [ ] Establish the approved client integration route to that candidate without
  changing the installed live client or exposing another public service.
- [ ] Start the candidate and record successful account/game entry, candidate
  identity, client identity and initial database state. No such run was made
  during this inspection.

## Ordinary-player acceptance

### First composed startup evidence

The separate game runtime is now staged at
`C:/Revora/DisposablePostgres/moon-box-candidate/game`: 471 hash-checked files,
113,963,688 bytes before the XML namespace correction. The staging tool selects
explicit catalogs, active map-index terrain and map caches; it does not copy the
live configuration, character progression, board state, logs or other executables.
Generated configuration uses `127.0.0.1`, Account 19558, Game 19559 and database
25439. The preserved catalog server key is `Slayer`. Only runtime/startup sections
are retained in the application config; database credentials come from the child
environment. The local manifest records hashes and explicitly makes no clean-build
claim because the build worktree contains owner edits.

`tools/Verify-MoonBoxDatabase.ps1 -CheckGameStartup` has now attempted actual
candidate game startup. The first attempt exposed an XML namespace hoisting error
in the staging tool. That generator was corrected; its previous config is preserved
at `game-config-before-namespace-fix.xml` and the manifest was updated against the
exact preimage. The second attempt reached quest initialization but failed before
Account/Game listeners opened with `Anniversary NPC identity collision`.

The staged source catalog contains legacy SOB NPC 900028, `AdmAniversario`, mesh
34240, on map 6000 at (102,78). `TwinCityPlaza` still parks/closes this ID, moving
it to (90,78); `AnniversaryQuest.PlaceNpc` requires the restored Talker profile
at Twin City (323,307), mesh 19936. This composed startup therefore needs an
explicit legacy-profile migration/identity resolution and a regression test;
the existing placement fixture did not include that legacy actor. Do not weaken
the collision guard for unrelated NPC profiles.

Payment/vote background workers also reported missing `identity.cp_orders` and
`identity.xtremetop_votes` in the isolated database. No live payment/vote data
should be copied merely to silence those workers. Candidate integration handling
remains to be resolved. Startup initializers have now changed candidate schema/
state, so the earlier exact 100-table empty-runtime check is historical and must
not be treated as a current post-startup schema expectation.

The second game process required a bounded forced stop after its startup failure;
PostgreSQL then stopped cleanly. No listeners remained on 19558, 19559 or 25439.
The redacted local log is `game-startup.log`. There was no native client login,
character creation or quest acceptance. Normal game shutdown remains unverified.
Map-index terrain IDs unresolved by the selected files/cache are 1511, 900000,
9960, 10080, 10081, 10082, 10133, 10136, 10201, 10160, 10162, 10166, 10221, 10230;
this is file-inventory evidence, not a finding that each blocks Moon Box.

### Corrected composed startup

Anniversary placement now replaces only the exact retired `AdmAniversario`
profile at its two observed jail coordinates. The plaza sweep leaves that
identity to the restored event. The isolated build and 551 Anniversary checks
pass, and an actual startup retry progressed beyond the collision.

That retry exposed the protection listener's hard-coded port 6319. The port is
now configurable as `ProtectionPort`, retaining 6319 when absent; invalid/zero
values are rejected. The private candidate uses 19619. A subsequent checker run
caught the socket implementation ignoring its supplied IP and binding all
interfaces; the owned candidate was stopped. `C_Socket.Server.Start` now binds
the supplied address, and protection receives the configured GameIP. Protection
remains enabled; its validation messages and behavior were not disabled.

The final bounded launch passed: Account 19558, Game 19559 and protection 19619
all listened on `127.0.0.1`; `@close` completed with exit code 0 and no forced
termination. No candidate game listeners remained afterward, and PostgreSQL
then stopped cleanly. The checker treats only its explicit four generated state
files as mutable; executable, config and catalog hashes remain checked. Previous
candidate binaries/configuration are preserved locally against verified preimages.

This proves composed startup/listener binding and normal process close only.
It does not prove a real Account/Game login, character creation, quest route or
client rendering. Payment/vote identity-schema dependencies remain unresolved
in the isolated environment; no production payment/vote data was copied.

### Private client endpoint preparation (2026-09-21)

`tools/prepare_moon_box_client.py` prepares only an executable/server-list pair
under `C:\Revora\DisposablePostgres\moon-box-candidate\client-profile`.
It does not copy assets, launch a client, change the installed tree, or publish
a patch. A complete private client workspace and real login remain pending.

The installed genuine DX9 executable still matches approved hash
`3728BC3FD4E584D85301EDD2B1887A176267CDAA6356038F9951EF23E4FDED60`.
However, installed `Server.dat` (`32D83D0DDEA3860AC9BE137738A32266B8E4B9041AC4424FB7DD1987049CE506`)
failed RSA type-one padding validation with that executable's public key.
This is an offline pairing result, not a diagnosis of current player logins.

The existing Revora derivative (`B6D6091CD026757BB9E6400557A44F72BA995D92586618D610861700F362D130`)
and its `RevoraServer.dat` (`E08C555D34B7ABF7341EDE94EC8867EAAEBE5A6F0347A64ECEC1E0EEEE616E2F`)
successfully validated using the existing owned ClientEvidence RSA codec.
The loopback derivative preserves this pair's RSA trust anchor and validation
code. No signing keys were accessed or generated.

The generator requires these exact source hashes, x86 PE32, the reviewed seven-byte
GetServerInfo prologue at file offset `0x11CEB6`, and 62 zero bytes at `0x7662E4`.
It adapts the existing endpoint-only owner-test hook recorded in
`C:\Revora\OwnerTest\6711\dx9-account-endpoint-patch.json`: type 2 returns
`127.0.0.1:19558`; other types execute the displaced prologue and resume.
The local manifest records both patch spans and exact hashes. Restoring those
spans reproduces the source byte for byte. Existing targets are refused.

Prepared output SHA-256:
`409E5499BA5DA33FDD3EE3C0D31F301AF52E936E196D4E6911C2ADB7BED78C9F`.
Capstone disassembly confirmed both branch destinations, the loopback address,
port immediate and original-prologue fallback. Mutated preimages were rejected.
The resulting pair again passed RSA decoding, producing the same 7,188-byte
XML hash `AC069DDFF006D3FF0FCE8DD51E3A2A4D7659F9E8F07FAD1059341B6E1B0FA532`.
Installed source hashes were rechecked unchanged. These are static checks;
ABI behavior, connection routing and player acceptance are not yet proven.
The output is a private derivative, not the genuine original or a signed release.
No proprietary binaries or decoded server lists are committed to Git.

### Native rendering preflight (2026-09-21)

The current agent session is Windows session 2, shown as disconnected by
`query session`; the installed video controller is Microsoft Basic Display
Adapter (driver 10.0.20348.1). `tools/CheckCandidateDx9.cs`, compiled as x86
with the installed .NET Framework compiler, called the system `Direct3DCreate9`
and `IDirect3D9.GetAdapterCount` in this session. It returned **zero adapters**
and exit code 1. This is a measured prerequisite failure, not an inference from
the adapter name alone. The probe does not launch or modify the game client.

Reproduce without rebuilding the server:

```powershell
& C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /platform:x86 /target:exe /r:System.Windows.Forms.dll /out:obj\chi-refill-validation\CheckCandidateDx9.exe tools\CheckCandidateDx9.cs
& .\obj\chi-refill-validation\CheckCandidateDx9.exe
```

When an adapter exists, the probe additionally attempts a hidden 64x64 windowed
HAL device with software vertex processing and releases its resources. Even a
passing result proves only a rendering prerequisite, not client compatibility.
No multi-gigabyte asset copy or client launch was attempted after this failure.
A connected owner-authorized session exposing DX9 rendering is needed to resume
this native gate. Server earning/persistence work can continue independently.

Use an authenticated non-GM character, level 70 or higher. Record observations
and redacted screenshots; do not record passwords, tokens or packet secrets.

| Check | Action and required observation | Result |
| --- | --- | --- |
| Entry | Visit Fortune Teller near Twin City 489,622; accept and reach Maggie in map 1042. Confirm dialogue and nearby actors render. | Pending |
| Hunting | Take Maggie's routes; defeat ToughHorn in the six tactics and obtain six distinct bound tokens. Record map transitions and readable combat/item presentation. | Pending |
| Persistence | Disconnect during an active run and reconnect; retain the same earned materials and saved destination. | Pending |
| Jade | Reach Death Tactic and exchange the six earned tokens for one Soul Jade; enter Life Tactic. Unrelated tokens must not count. | Pending |
| Spirit | An incorrect wandering ghost must not consume the jade. Locate the correct ghost through normal play. | Pending |
| Capacity | Fill the bag before accepting a reward. The claim and jade must remain usable; free enough space and retry. | Pending |
| Box ending | Receive one Moon Box and one bound catalog P7 soul on a new contract, return to town, and receive achievement 11022. Inspect the actual client binding indicator. | Pending |
| Replay/reload | Repeated claim attempts grant nothing extra. Reconnect and retain the same item UIDs and achievement; correlate with the single saved result receipt using read-only database inspection. | Pending |
| Meteor ending | On another new run choose three Meteors: receive three Meteors plus one bound P7, with no new Moon Box achievement from this ending. | Pending |
| Refusal | On another run refuse the spirit: consume the jade, return to town and receive no reward or achievement. | Pending |
| Restart | Gracefully restart the isolated candidate with an active run and a completed run present; verify both recover correctly through normal login. | Pending |

Existing `MoonTests` and `VerifyIslandSoul` cover ordinary-player dispatch,
transaction rollback, concurrent claims, inventory reload and persisted bound
P7 ownership tied to receipt UIDs. They do not prove native rendering, network
login or a real process restart. Reuse those results unless candidate changes
justify a focused `-MoonOnly` run; no Arena rerun is needed for this document.

Keep native acceptance and deployment unchecked until the observations exist.
After acceptance, prepare the coherent release and its backup/rollback plan;
do not describe a full-server candidate as a Moon-Box-only deployment.


## Socket acceptance update (2026-09-27)

Actual Account/Game socket entry now passes for the ordinary earned-career
character. Fortune Teller entry and Maggie dialog access pass, and two complete
game/database restarts retain the identical saved portal run, with no materials,
counters, payment or reward added. [Evidence](ROADMAP_STARTUP_ACCEPTANCE.md#ordinary-moon-box-entry-and-durable-resumption-2026-09-27).
This does not complete the native table above or prove the socket combat/reward
journey. Those checks remain pending; no production deployment occurred.


### Combat and six-token socket evidence (2026-09-27)

The same ordinary character now earned six distinct bound tokens from 27 real
ToughHorn kills. Interrupted combat resumed without resetting the run; two
actual game/database cycles verify exact token UIDs, bag ownership, binding,
permanence, saved kill counts and unchanged run fingerprint after reload.
[Evidence](ROADMAP_STARTUP_ACCEPTANCE.md#six-moon-box-tokens-earned-through-combat-2026-09-27).
Jade, final rewards, achievement and native table observations remain pending.


### Earned Moon Box ending and promotion (2026-09-27)

The ordinary socket journey now includes all six combat-earned tokens, Soul
Jade exchange, 11 wrong-ghost refusals, correct-ghost Moon Box/bound P7 award,
and consumption of that exact box for the final Trojan promotion. Two real
restart cycles retain the single completion receipt, permanent bound catalog
P7, class 15, DragonBall and earned achievements. [Evidence and exact scope](ROADMAP_STARTUP_ACCEPTANCE.md#earned-moon-box-completion-and-final-trojan-promotion-2026-09-27).
The native table, alternative endings and deployment remain pending.
