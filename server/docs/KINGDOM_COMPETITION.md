# Revora competitive Kingdom edition

Status: ordinary earning, atomic union membership/offices, cold crown persistence
and explicit opt-in full server startup are tested privately. An earned crown now
survives full server/database restart and ordinary authenticated wire login.
Startup remains disabled by default; native acceptance and live deployment remain
pending. Interrupted admissions also recover through full restart/authenticated
login with activation disabled, as documented below.
A private ordinary Kingdom earning journey now passes; no deployed route is claimed. The owner selected competitive
selection on2026-09-30; this document defines the Revora implementation edition.

## Event contract

- One daily contest,17:00 inclusive to18:00 exclusive in Pacific time, retaining
  the legacy advertised hour while using an explicit timezone.
- An eligible union member registers once per contest. Switching unions cannot
  change that character's scoring allegiance during the same contest.
- Harvesting has a five-second server-owned operation and thirty-second callback
  deadline. A character can carry one brick. Only a deposited brick scores one
  point; buying GoldBricks or having a lifetime brick balance gives no event score.
- Death, leaving the field and interrupted-harvest recovery must discard unbanked
  cargo. The field adapter now handles these transitions and restores prior PK mode.
- Highest deposited total wins. Equal totals use ascending union ID. The winner
  must still exist with a saved leader who belongs to that union at settlement.
- Empty contests produce no ruler. Results record the winning union, leader, score
  and expiry at the following day's18:00. Repeating settlement cannot change these
  fields or renew expiry. A delayed result is historical; future title projection
  must refuse an already expired crown.
- Entrants receive ten seconds of protection in both combat directions. After
  protection, Union-mode attacks can hit admitted opposing unions in this field;
  shared flight, target protection and attacker cooldown checks still apply.
  Event kills grant no legacy kill points, currency or item drops.
- No entry payment or additional currency prize is introduced. Overlord/Solar
  Dragon earning and succession will consume the durable result after integration.

## Implemented persistence

`KingdomCompetitionLedger` in `PlanderWar.cs` creates separate contests, entries
and accepted-delivery tables. All its operations use a bounded transaction lock
before entity/union rows, parameterized SQL and UTC time inputs. Membership is
checked against saved entities. A harvest operation belongs to one admitted UID
and union. Pending callbacks cannot be replaced early or completed twice. Removing
cargo and recording its delivery occur in one transaction. A failed receipt rolls
back both actions; a duplicate callback cannot add a point.

Settlement derives scores only from that contest's accepted-delivery receipts,
locks the selected union/leader records and writes the result once. Failed result
writes remain retryable. Npgsql's timezone-aware timestamps are converted from
returned Local time to UTC; relabeling Local values as UTC would settle early.

The field adapter holds the current session lease and inventory lock, validates
ordinary-player identity and NPC proximity, and rejects trade/booth interactions.
Harvest work cancels on movement, death, departure or timeout; a changed death
stamp also detects death followed by revival between pulses. Return atomically
clears unbanked work and saves a safe town position before runtime projection.
Login recovers interrupted admissions. Character union hydration now restores
runtime and spawn-packet union identity without database setters or broadcasts.
The field remains disabled until the remaining union/authority integration passes.
Legacy `PuntosUnion` and GoldBrick exchanges give no competitive score. Paid/login
selection and the highest-lifetime-bricks selector have been removed; startup
ignores the legacy configured Kingdom ID.

## Validation

Isolated x86 Debug build passed with existing warnings. `tools/CheckKingdomLedger.cs`
passed31 checks across two fresh executable processes with a PostgreSQL stop/start
between phases. It uses only the ownership-marked `revora_kingdom_ledger_fixture`
schema on the fixed loopback25439 candidate. The final phase removes the schema;
PostgreSQL stopped afterward. These are database-boundary tests with supplied
membership rows and clocks, not ordinary NPC/combat or full Host acceptance.

Verified: calendar boundaries, one contest per day, saved membership and cross-union
refusal, five-second timing, forged/cross-owner operation refusal, eight concurrent
completion/deposit calls yielding one result, receipt-failure rollback, failed
settlement retry, deterministic ties despite different lifetime brick balances,
closed-window refusal, exact result recovery, immutable winner/expiry, discard and
stale-callback refusal, allegiance preservation and an empty contest producing no
ruler. Build SHA-256:
`F08009344414E6E4EE81613BC2833BEECA97CD5C2C82340A1C88DB00D4EC22CF`.
Ignored logs: `obj/kingdom-ledger-build.log`, `obj/kingdom-ledger-capture.log`,
`obj/kingdom-ledger-verify.log`. The probe uses the existing private DPAPI child-
environment runner and binding redirects; never put credentials on a command line.

## Field integration findings and remaining work

The private3998.map is836x836, SHA-256
`89224a6f4438ee8ced0f2f63d6643961f7e21387cf2be8e092a8dd3984f9de67`.
Correction to the original raw-byte audit: byte1 is blocked and byte0 is walkable;
the Floor setter semantics had been read backwards. There are453,316 blocked and
245,580 walkable cells. The cardinal component from296,153 contains245,579 cells.
Old anchor114,300 is blocked;500,300 is walkable. The earlier claim of disconnected
central/outer walkable components was incorrect.

Placement now uses the loaded `Floor` API. Arrival is3998(296,153); depot is
(300,153), and quarries are(500,300),(151,300),(300,498). A bounded flood fill checks
all approaches with actor tiles excluded. Historical entry1002(317,324) and
return(319,324) are superseded by the footprint correction below.
NPC IDs903980..903984 avoid legacy store, tournament and Nobility donor IDs;
initialization rejects collisions across loaded maps before adding any actors.
The installed town compact floor is entirely open, so its placement checks are
server-floor evidence only, not proof of native-client obstacle accuracy.
No terrain assets are copied to Git.

`tools/CheckKingdomField.cs` passed25 checks through ordinary non-GM NPC handlers,
loaded terrain and normal character reload. It covers admission, all three
quarries, movement cancellation, bank proximity/replay, simulated death-stamp
change, prior PK-mode restoration, closing settlement, re-entry after login and
failed return-position write rollback. Union membership, clock and death stamp
are controlled fixtures; this is not union creation, actual combat or native play.
The return-failure injection exercises the ledger boundary, followed by normal NPC
exit. Private field tables are removed and PostgreSQL stopped after the probe.
The31 ledger checks also passed again across fresh processes and PostgreSQL restart.
Current isolated x86 assembly SHA-256:
`7033CB121935C4593E8959E73AA024015A5CDF3A686993EAE35E4D0872A39C5E`.
Logs: `obj/kingdom-field-build.log`, `obj/kingdom-field-tests.log`, and the ledger
capture/verify logs. The field probe uses the same fixed private candidate and
DPAPI child environment, plus x86 `KingdomField.Helpers.exe` compiled from the
committed PotencyHunt helper sources. No live configuration or deployment changed.

The legacy city-only Union attack branch and both legacy death-point branches
have now been removed. The field denies unadmitted, same-union, cross-map, stale,
fake, closed-window or protected combat. Direct ReceiveAttack also applies the
normal shared flight/attackability/cooldown checks; the Union event flag grants
no city attack permission. Field deaths use the ordinary player death flags and
packet path while bypassing legacy crime/economy/kill-credit behavior. The generic
item-drop entry and Grabber rune's separate drop branch both exclude this field.
Dead entrants retain their admission for revival; carried bricks are discarded.
Neither the legacy open flag nor normal event startup is enabled.

## Combat validation (2026-09-30)

The expanded field probe passes47 checks, including the previous NPC/recovery
journey plus actual nonlethal/lethal public combat, allied/self denial, direct
same-union death denial, ten-second entry protection, fake participant denial,
flight/target protection/cooldown checks, Peace mode, cross-boundary denial,
legacy flag isolation in town, two deaths separated by revival and closing-time
refusal before the return pulse. Exact persisted balances and item rows remain
unchanged using nonzero wealth and an unbound item; a black-name victim and attacker
with Grabber cannot trigger field item loss. Death and replay create no deposit.

The x86 isolated build passed with existing warnings. Assembly SHA-256:
`8213D2EA14F3B52F2D5F10609FD5072906BD1E2593B77D80BAE8F30517DBF704`.
Logs: `obj/kingdom-combat-build.log`, `obj/kingdom-field-tests.log`.
This uses real server handlers with fixture-supplied union membership and clock;
full Host/process combat recovery, native-client acceptance and live deployment
remain pending. Ledger code was unchanged in this batch; its prior31 restart
checks remain historical evidence, not a newly claimed full event restart.


- [x] Durable per-contest membership, harvest, deposit and immutable result storage.
- [x] Duplicate/rollback and separate-process/database restart checks at ledger scope.
- [x] Loaded-floor entry/field placement and private ordinary NPC admission/harvest/deposit checks.
- [x] Exit, interrupted login and prior PK-mode recovery; atomic failed-return rollback.
- [x] Private field-only opposing-union combat, protected entry, death/exit/cancel handling.
- [ ] Atomic union creation and succession, authoritative result projection, removal
  of paid/login selection, immediate old-ruler revocation and new-ruler wardrobe.
- [ ] Full ordinary event journey, interrupted Host recovery and replay checks.
- [ ] Genuine6711 client acceptance and deployment.

## Atomic union formation (2026-09-30)

The ordinary action7 route preserves the existing rule: a current guild leader
with neither a character union nor a guild union may form a union without an
added fee. It verifies the saved character rank/membership and saved guild leader,
uses the shared guild leadership gate, current-session lease and owner lock, and
rejects duplicate names. A bounded database transaction saves the complete union,
founder office, guild link and allocator together. The ID is above both the saved
allocator and all existing union IDs. No currency balance changes.

Cache publication uses a new dictionary, preserving concurrent readers of the old
snapshot. A committed projection updates the founder's runtime/spawn fields and
existing overhead message without database setters. Precommit failures roll back
all writes; postcommit projection failures require reconnect, which restores the
saved union and office without a second allocation. Existing transfer, membership,
office and crown paths are not made atomic by this creation change.

The isolated x86 build and14 private formation checks pass, plus all47 field/combat
checks on the same assembly. Formation covers malformed frames, ordinary member
and fake-founder denial, saved-rank disagreement, concurrent replay, duplicate
names, forced guild-link rollback and retry, injected postcommit projection failure,
normal character login and exact union-cache reload. Guild leadership is a supplied
fixture prerequisite; this does not claim a complete guild-creation-to-crown route,
a fresh Host process restart or native-client submission/display.
Assembly SHA-256:
`09FDE83C5B7D3E47F2C317DDD064596C02E16D065FEA3DCD42ED653794EFBC0F`.
Probe: `tools/CheckUnionFormation.cs`, using the existing x86 helper assembly and
fixed private candidate. It owns/removes `revora_union_formation_fixture`; PostgreSQL
stops afterward. Logs: `obj/union-formation-build.log`,
`obj/union-formation-tests.log`, `obj/union-formation-field-regression.log`.
No live database, restart, client release or deployment changed.

## Atomic union succession (2026-09-30)

Action14 now requires current ordinary owner/target sessions and matching saved
membership, names, office fields and leader identity. The online same-union target
requirement is preserved. A bounded transaction commits the new leader, clears both
participants from office arrays, demotes the prior leader and assigns the successor
one leader office. Saved array preimages must match the loaded union. Currency and
union economic balances are untouched.

Union packet dispatch and direct legacy membership/office mutators share a runtime
gate with formation/succession, preventing old-leader authorization from racing a
transfer. Stale union objects are rejected. Per-office appointment/removal cannot
overwrite the leader's unique office. Session leases and ordered nonblocking owner
locks protect both players during transfer. After commit, office/spawn/overhead
state is projected without persistence setters and existing wardrobe predicates
are reconciled. A projection failure fences both players for ordinary reconnect.

This serialization does not make all legacy membership/office writes transactional.
Their wider failure recovery still needs completion. Existing predicate reconciliation
is not evidence of competitive crown earning, expiry or full title/wing succession.

`tools/CheckUnionSuccession.cs` passes25 checks combining formation with ordinary
allegiance joining, core/guard appointment, succession, nonleader/foreign/self
refusal, concurrent replay, forced character-write rollback, prior-leader authority
loss, protected leader office, ordinary login and injected postcommit recovery of
both sessions. A fresh union cache retains exact saved rows. All47 field/combat
checks also pass on the same x86 assembly; no native or full Host restart claim.
Assembly SHA-256:
`AA7466E512C5D0D52C70A25ABA03643362379082C65D6A7C7BBEA41EC32C0051`.
The probe owns/removes `revora_union_succession_fixture` on the fixed private
candidate, using the existing x86 helper assembly/DPAPI runner; PostgreSQL stops
afterward. Logs: `obj/union-succession-build.log`, `obj/union-succession-tests.log`,
`obj/union-succession-field-regression.log`. No live deployment changed.

## Competitive crown and wardrobe authority (2026-09-30)

`ActiveCrown` derives authority from the latest finalized contest whose end has
arrived, its immutable expiry, and the winning union's current saved leader with
matching saved union membership and leader office. A newer empty result produces
no crown; the reader never falls back to an older winner. Succession changes the
current ruler without rewriting the frozen contest winner or extending expiry.

`KingdomCrown` publishes that authority and reconciles Overlord11000 and Solar
Dragon40011000 for ordinary current sessions. The old/new rulers are prioritized
for notifications; remaining online presentations drain in bounded batches.
Eligibility checks expiry immediately, including between timer pulses. Authority
read failure clears authorization; recovery reloads the same result. Recurring
reads occur on the field's five-second settlement cadence with a five-second SQL
command timeout. Title requests cannot use a manually assigned legacy Kingdom ID.

Paid promotion option99 no longer debits RVPs or union funds. Login thresholds,
legacy event closure and highest-lifetime-brick selection no longer choose a
Kingdom. Startup clears the old configured selector. Existing brick-to-fund
exchanges remain separate from competitive scoring, and their dialogue no longer
claims to award Kingdom status. No new entry charge or currency reward is added.

`tools/CheckKingdomCrown.cs` passes40 private checks through creation, ordinary
allegiance and appointment, field harvesting/deposit/settlement, reward ownership
and equip, succession/revocation, failed-write and postcommit reconnect recovery,
fixed expiry, clock rollback, another earned winner, empty results and historical replay. It also
injects an authority-read failure and confirms recovery cannot renew expiry.
The changed title suite passes568 checks, and the field/combat regression passes47.
The isolated x86 assembly SHA-256 is
`05F882869F3F537F32F23F4232B038CF59F9D01F6AF7A657856C891B02F87A8D`.
Logs: `obj/kingdom-crown-build.log`, `obj/kingdom-crown-tests.log`,
`obj/guild-kingdom-crown-title-regression.log`, `obj/kingdom-field-tests.log`.
The crown probe owns/removes `revora_kingdom_crown_fixture`; the title suite owns
its separate private schema. PostgreSQL stops after each run.

Normal event startup remains inactive. These are real server handlers with supplied
guild-leader eligibility and controlled calendar, not a native client session or
fresh full Host restart. Remaining legacy membership/office transaction acceptance
still needs verification before activation. Cold authority/wardrobe recovery is
covered by the additional isolated check below.
Neither Kingdom wardrobe row is marked deployed or fully complete by this batch.

### Cold crown and wardrobe recovery (2026-09-30)

`tools/CheckKingdomCrown.cs capture` passes29 checks, saving the ordinary guildless
successor after a real handler-driven win, leadership transfer and reward equip.
After stopping and starting the private PostgreSQL instance, `verify` passes10
checks in a separate executable process. It checks both the process identity and
PostgreSQL start timestamp, exact saved contest and wardrobe rows, normal character
loading, current successor authority, equipped Overlord/Solar Dragon visuals,
settlement replay without expiry renewal, and removal at the original deadline.
No-argument execution retains the complete40-check journey.

The verifier hydrates union, arena and wardrobe caches through their normal
loaders before character login; guild eligibility was supplied during capture.
This proves fresh-process reward recovery, not complete Host startup, socket
login, guild roster reconstruction or genuine DX9 rendering. The event remains
inactive at normal startup and the live server was not changed.

Both phases require the fixed private database and an ownership-marked schema.
Capture refuses an existing schema; failed recovery preserves evidence, and
successful verification removes only the owned fixture schema. The private runner
stops PostgreSQL after each phase. Logs: `obj/crown-capture.log` and
`obj/crown-verify.log`. The tested server assembly hash is unchanged from above;
only the validation probe and documentation changed.

### Atomic voluntary union membership (2026-10-01)

The ordinary guildless allegiance and voluntary departure handlers now use
`UnionMembership.Change`. A session lease, character lock, union mutation gate,
bounded database advisory lock and one transaction protect the saved character
membership plus all three union office arrays. Saved identity, guild membership,
offices, leader and array preimages must agree with the current owner and cache.
Null saved guild IDs are treated as no guild, matching the character loader.
Unknown union IDs are refused safely; a ruler must transfer leadership before
leaving. The existing one-minute rejoin cooldown remains session-local; this
batch does not claim it survives reconnect.

Only a committed transaction changes cached membership, office arrays and spawn
identity. Departure now clears the in-memory union ID as well as the saved row;
previously the legacy removal left the in-memory ID stale. Projection failure
fences the session, allowing normal character login to recover the committed
result. No membership success response is emitted before commit. Existing
overhead and spawn packet families are reused; native visual acceptance remains
pending.

`CheckKingdomCrown membership` passes30 checks including its15 setup checks.
Coverage includes ordinary join, unknown/stale-guild denial, ruler departure
refusal, appointed offices, suppressed union and character writes, concurrent
departure, session cooldown, save/reconnect, and injected postcommit failures for
both join and leave. The full40-check crown journey and29/10 capture/restart
checks also pass on the isolated x86 assembly
`5CC1EB148A1DAEAE4AEC0C8CAFABBFB0272A5678CE320C303B8E43EF5ED7D959`.
Logs: `obj/union-membership-build.log`, `obj/union-membership-tests.log`,
`obj/crown-journey-regression.log`, `obj/crown-capture.log`, `obj/crown-verify.log`.

This does not make legacy guild-wide joining/removal, leader-driven expulsions
or office appointment/removal atomic. Those paths, full Host/native acceptance
and startup activation remain pending. No live server or client was deployed.

### Atomic individual expulsions (2026-10-01)

The ordinary union leader's `ExpelMember` request now calls `UnionExpulsion.Remove`
for both online and offline targets. Both character session leases prevent login
replacement during the operation; online character locks are acquired in UID
order without waiting. Saved ruler membership, office, union leader UID/name and
target membership are checked inside the bounded transaction. The target cannot
be the ruler, an outsider or an absent character. All three office arrays and
the target's membership/office fields commit together. Online spawn identity and
title predicates update only after commit. A postcommit failure fences affected
online sessions; normal login recovers the durable result. Offline targets are
updated without creating an artificial online session.

`CheckKingdomCrown expulsion` passes32 checks (15 setup,17 expulsion checks).
It covers stale saved authority, online/offline rollback, concurrent requests,
save/reconnect, projection failure and contention with a target login lease held
by another thread. The30 voluntary-membership and40 crown journey regressions
also pass. Isolated x86 assembly SHA-256:
`5FA01678322E8E5425E12BFE003B6AA90C20008506F2E91CBD386ABB576CF3A5`.
Logs: `obj/union-expulsion-build.log`, `obj/union-expulsion-tests.log`,
`obj/expulsion-membership-regression.log`, `obj/expulsion-crown-regression.log`.
The owned private schema was removed and PostgreSQL stopped after each run.

Guild-wide joining/removal/expulsion and office appointment/removal still use
legacy multi-write paths. Their transaction recovery, full Host/native acceptance
and event startup activation remain pending. No live deployment was performed.

### Atomic guild-wide union requests (2026-10-01)

`GuildJoinLeague`, `GuildQuitLeague` and `ExpelGuild` now use one transaction for
the guild link, affected saved characters and all union office arrays. Complete
saved/cache roster identity, guild leader authority, union ruler authority where
required, member affiliations and office preimages are checked. Both online and
offline members receive session leases; online character locks are nonblocking
and ordered by UID. The shared guild gate prevents legacy guild admission,
removal and dissolution from changing the roster during the operation. Member
updates use a parameterized bulk statement with an exact affected-row count.

Joining refuses a member affiliated with another union. Withdrawal/expulsion
only removes members affiliated with this union and preserves independent
affiliations. Removing the ruler's guild requires leadership transfer outside
that guild first. A union-linked guild must withdraw before dissolution, closing
the old dissolution shortcut through nontransactional union removal. Successful
commits update the guild association and online spawn/title projections; failed
postcommit presentation fences online participants for normal login recovery.

`CheckKingdomCrown guild` passes33 checks, including15 common setup checks. The
mixed roster exercises a guild leader, online member and offline member; invalid
authority/rosters, foreign affiliations, suppressed middle character and final
guild writes, concurrent requests, save/reconnect, dissolution protection,
voluntary withdrawal, expulsion and postcommit failure recovery are covered.
The30 membership,32 expulsion and40 crown journey regressions pass. Final isolated
x86 assembly SHA-256:
`73312E1F46208A56FA69133E87A11DAE2DB57D17E85E420EF3503B89378BD400`.
Logs: `obj/union-guild-build.log`, `obj/union-guild-tests.log`,
`obj/guild-membership-regression.log`, `obj/guild-expulsion-regression.log`,
`obj/guild-crown-regression.log`. Producer, missing-work and career audits pass
without changing achievement completion counts.

This is not complete guild recruitment persistence: individual recruitment via
advertisements still changes guild and union membership through separate legacy
writes (`Network/PacketHandler.cs`, case2227), and other recruitment routes need
consistent union policy. Office appointment/removal, complete Host restart and
native-client acceptance remain pending before event activation. The private
schema was removed and PostgreSQL stopped; no live deployment was performed.

### Atomic individual guild admission (2026-10-01)

All four existing recruitment callers now reach `GuildAdmission.Join` through
`Guild.AddMember`. Advertisement admission no longer follows the saved guild
write with separate union removal/addition. One bounded transaction validates
saved character guild/union/office preimages and the saved destination guild's
allegiance, then saves the guild, member rank and union affiliation together.
When joining the union for the first time, stale office-array references are
cleared in the same transaction. Existing offices in the same union are retained.
Session, character, guild and union gates protect current ownership; online
membership and spawn identity are projected after commit. A failed projection
fences the applicant, and the committed roster supports ordinary reconnect.

Documented Revora recruitment rule: applicants must have no independent union
or already belong to the destination guild's union. A conflicting affiliation
is refused with an instruction to leave first, rather than silently removing
the player from their current union. The existing session-local union cooldown,
guild blacklist and recruitment requirements apply. This does not add a durable
cooldown or claim a complete atomic guild lifecycle. The obsolete union member
and guild mutation methods, now without callers, were removed.

`CheckKingdomCrown admission` passes32 checks (15 setup,17 admission checks).
Real advertisement and both application/invitation acceptance orders are covered,
along with the alternate advertisement handler, requirement/blacklist/cooldown
denials, conflicting affiliation, stale saved state, suppressed writes, concurrent
save/replay, same-union office preservation and postcommit reconnect recovery.
The33 guild-wide and40 crown journey regressions pass. Producer, missing-work and
career audits pass with unchanged completion classifications. Assembly SHA-256:
`8839512A332826EAB04A610499F4E0C87575B5E4571AAF233D7D69EAA1AF4486`.
Logs: `obj/guild-admission-build.log`, `obj/guild-admission-tests.log`,
`obj/admission-guild-regression.log`, `obj/admission-crown-regression.log`.

Guild identity tokens/study rewards remain separate unfinished work; this batch
does not enable them. Union office transactions, full Host recovery and native
acceptance still precede event activation. The private fixture was removed and
PostgreSQL stopped. No live server or client was deployed.

### Atomic union offices (2026-10-01)

The core-official, palace-guard and harem appointment/removal routes, including
their aliases and supported core/guard resignations, now use `UnionOffices`.
The current ruler's saved authority, target membership and office preimages,
canonical union and all saved office arrays are checked under character session
leases, nonblocking owner locks and the union mutation gate. One bounded database
transaction saves the office arrays and character's office fields. Online
presentation occurs only after commit; offline removals recover through normal
character login. Postcommit failures fence affected sessions. Obsolete separate
office mutation methods were removed.

Appointments retain the existing online-target requirement and never displace
an occupied seat. Exact source-defined rank IDs replace the old broad numeric
range checks: eight core seats,20 guards and10 harem seats, including the401/402
automatic harem groups. Undefined/overflowing rank IDs and malformed names are
refused. A full guard roster now refuses an extra appointment instead of indexing
past its final slot. Resignation affects only the requesting member's selected
office and cannot remove the ruler's leadership. These are source-level rules
and server-handler evidence, not new claims of native protocol verification.

`CheckKingdomCrown offices` passes38 checks (15 setup,23 office checks): all three
families, exact slots/aliases, capacity, concurrent replay, occupied seats,
saved authority, rollback at both writes, offline removal, resignation,
postcommit failure and reconnect. The dependent membership30, expulsion32,
guild-wide33, recruitment32, crown40 and capture/restart29/10 checks also pass.
Producer, missing-work and career audits retain their prior classifications.
Isolated x86 assembly SHA-256:
`7FAE1505CEDE8F878461989C56002FD756B0062F16C5C719878F7A9CDCBB0562`.
Logs: `obj/union-offices-build.log`, `obj/union-offices-tests.log`, and
`obj/offices-{membership,expulsion,guild,admission,crown,capture,verify}-regression.log`.

The private schema was removed and PostgreSQL stopped after each run. Full Host
recovery, genuine6711 client acceptance and normal event activation remain
pending; no live deployment occurred and the Kingdom rewards remain staged.


## Opt-in full server startup and actor footprints (2026-10-01)

`REVORA_KINGDOM_COMPETITION_ENABLED=true` initializes the staged event after
map, union and wardrobe prerequisites, before listeners and tournament startup.
Missing, empty or false leaves it disabled; malformed values fail startup before
activation. This is a startup-only setting, not a runtime toggle. No live setting
was changed.

Full catalog startup exposed the GuildController at1002(319,325): its3x3 NPC
footprint blocks the previous return and approach. UnionMarshal now stands at
1002(314,325), with town approach and saved return at1002(316,325). Field validation
reserves each NPC's full3x3 footprint and proves connected interaction positions
two tiles east, then verifies these positions after actual actor placement.
Interrupted admission recovery uses the same saved return coordinates.

Private validation on isolated x86 assembly SHA-256
`0E80B6DA3BE319539C3CBAA78CBFB019B1BB74353172502832568BD017A0963E`:

- 51 field/NPC/combat checks, including missing/malformed startup settings and the
 real GuildController/GuildDirector footprints.
- 40 ordinary crown journey checks and29 capture plus10 cold recovery checks after
 a separate executable process and PostgreSQL restart.
- Two complete legacy Revora.exe startup/shutdown cycles with opt-in enabled and
 two with it disabled; PostgreSQL was restarted between cycles. All three private
 listeners remained loopback-bound, shutdown exited0 without an emergency kill,
 and listeners disappeared. The enabled marker confirmed map3998 and five actors.
- All three achievement producer/missing-work/career audits passed; classifications
 are unchanged.

Evidence is retained in ignored local logs `obj/kingdom-field-tests.log`,
`obj/crown-journey-regression.log`, `obj/crown-capture.log`, `obj/crown-verify.log`,
`obj/kingdom-enabled-startup-{1,2}.log` and `obj/kingdom-host-startup-{1,2}.log`.
The startup checker now exposes `-CheckKingdomStartup` and verifies the expected
ready/disabled marker.

These full server cycles had crown=0. They do not prove earned crown recovery
through full server startup and authenticated player login; the separate cold
probe remains the earned-state evidence. Genuine6711 client acceptance and live
activation/deployment remain pending. This does not claim modern Revora.Host
lifecycle validation.


## Recovery when activation is disabled (2026-10-01)

Startup with activation off now checks once whether the admission ledger exists.
The normal character loader can return interrupted admissions even while the
entry NPC, scoring and event pulse remain disabled. Return still atomically
clears cargo and saves1002(316,325), and restores the saved pre-event PK mode.
A never-enabled installation does not need a new ledger merely to remain disabled.
Database lookup errors fail startup rather than silently skipping recovery.

The field probe passes55 checks, including an ordinary entrant saved with cargo,
a simulated fresh disabled startup, normal character reload, persisted return,
PK-mode restoration and replay without admission or score creation. The existing
40 crown checks also pass. This is focused handler recovery evidence; the probe
resets activation state within one process and does not establish full authenticated
socket recovery after a restart. That roadmap gate remains pending.

Isolated x86 assembly SHA-256:
`B9390EC4EB41D0A4135F8DE2C15E5AB12F23D045679C41C2C214475EDB9F3A33`.
Build retains existing legacy warnings. No live deployment or setting was changed.

Two additional full private server startup/shutdown cycles with activation off
and a PostgreSQL restart between them pass on this assembly. Both confirm all
three loopback listeners, the disabled marker, clean exit0 and no emergency kill.
These cycles validate startup integration without an interrupted logged-in entrant.


## Earned crown through full server restart and wire login (2026-10-01)

`CheckCandidateKingdomLifecycle.cs` extends the existing full-server harness with
`kingdom-capture` and `kingdom-reload`. It uses the owned disposable database
`revora_candidate_arena_process`, cloned from the private catalog database and
marked `revora-owned-kingdom-host-v1`. It refuses an unrelated database or occupied
fixture character/guild/account. Candidate configuration is restored after each
process, and listeners are checked for loopback binding.

The capture process runs the real Server.Run startup, supplies one eligible
ordinary guild leader, then creates a union through the normal packet handler.
It admits the player through UnionMarshal, harvests and deposits through the
ordinary quarry/keeper handlers, settles the contest, equips Overlord and Solar
Dragon and saves. Calendar/time and the initial guild/character eligibility are
controlled test inputs; guild earning and native client input are not claimed.
No crown result or wardrobe ownership is inserted directly.

A separate SRP/DH wire client authenticates the Player account, completes the
normal character/world login, receives both owned/equipped rewards and safely
replays equip. After graceful server shutdown and a real PostgreSQL restart,
a fresh full-server process repeats that login against the saved account. It
asserts changed process/database startup identities, restored winning union,
safe town position, received wardrobe state and an identical persisted contest
record, including original winner and fixed expiry. This checks the winning
leader; successor recovery remains covered by the separate cold successor probe.

The full catalog exposed a production bug: new characters can have NULL union
fields. Union formation previously converted that value directly and failed.
The saved eligibility read and conditional update now consistently treat NULL
unionid as unaffiliated, matching ordinary load behavior, while preserving exact
rank, guild ownership, atomic allocation and replay checks. The40-check crown
regression now starts its founder with NULL union/office fields and passes.

Capture and reload both pass on isolated x86 assembly SHA-256
`5C8FA4FE3F0E06FC3C55F2BFDC0B123820CA36335F96BFEC87FBA91847488EC7`.
All three achievement audits pass unchanged. Redacted evidence is retained in
`obj/kingdom-earned-host-1.log`, `obj/kingdom-earned-host-2.log` and
`obj/crown-journey-regression.log`. Existing legacy build warnings remain.
No live server setting, deployment or client package changed. Genuine6711 native
acceptance and full restart/login of an interrupted admission remain separate gates.


## Interrupted admission through full restart and disabled wire login (2026-10-01)

The full-server harness now supports `kingdom-interrupt` and `kingdom-recover`
after the earned-crown run. The ordinary character enters the next contest through
the clerk and completes a quarry harvest. Its carried cargo and map3998 position
are saved, and its offline fixture session is removed without returning the
admission. The server shuts down gracefully; this constructs persisted interrupted
session state, not a forced operating-system crash.

PostgreSQL is stopped and restarted, then a fresh full-server process starts with
Kingdom disabled. Before login, the probe verifies distinct process/database
startup identities and the still-open carried admission. A real SRP/DH wire client
logs in and completes world entry. While that connection is alive, the server
asserts ordinary Player/non-fake identity, town1002(316,325), restored Peace PK mode,
returned ledger state and cleared cargo/nonce. The database retains the safe town
position, and total deposits remain exactly at the checkpoint value. No recovery
step manufactures a point.

Both phases pass against the same production assembly as the prior earned-crown
run (`5C8FA4FE3F0E06FC3C55F2BFDC0B123820CA36335F96BFEC87FBA91847488EC7`).
Only private probes changed in this batch; no server rebuild or live restart was
needed. Redacted evidence: `obj/kingdom-interrupted-host-1.log` and
`obj/kingdom-interrupted-host-2.log`. The private database is stopped afterward.
Native6711 acceptance and live activation/deployment are still pending.


## Nullable membership and office persistence (2026-10-01)

The actual catalog's nullable affiliation fields also affected ordinary guildless
union admission: the pre-fix build reproduces an InvalidCastException and refuses
a legitimate join. Similar direct conversions and NULL-sensitive conditional
writes existed in guild recruitment, guild-wide admission and office lifecycle
transactions. Normal entity loading already interprets these unset fields as zero.

Saved membership/office reads now use COALESCE for nullable unionid, offical,
harem and guards. Conditional admission writes use the same zero interpretation;
office/expulsion non-ruler guards treat an unset office as zero. Nonzero values,
saved ruler identity, ranks, cache comparisons, session leases, ordered locks,
affected-row assertions and transaction rollback remain enforced. Existing rows
are not bulk rewritten or granted affiliations merely because a value is NULL.

The strengthened fixtures retain unset NULL fields for a guildless successor,
new guild applicant, mixed online/offline guild roster and empty officer slots.
Recruitment failure checks preserve the exact character JSON preimage, including
NULL values, across refused writes. Membership30, recruitment32, guild-wide33,
expulsion32, offices38 and crown40 checks pass:205 focused assertions. The old
failure is retained in `obj/nullable-union-reproduced.log`; successful phase logs
are the existing union/guild/crown logs, with runner output under
`obj/nullable-*-run.log`.

Isolated x86 build passes with existing legacy warnings. Assembly SHA-256:
`37E6849089FBA067414F5D9E44A4BE332038904521DC5A822B7F924274652C6F`.
This batch uses the disposable-schema ordinary-handler probes, not another
full-server/native acceptance claim. No production restart/deployment or default
activation change was made; private PostgreSQL is stopped afterward.
