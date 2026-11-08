# Guild Beast earning path: availability prerequisite

Status: incomplete; achievement 10303 must not be counted as implemented.

## Installed evidence (2026-09-25)

The isolated Guild Beast fixture copied family 7006 spawn rows for map 1038
from the installed public catalog into `revora_potency_validation.monsterspawns`.
The production `Map.LoadMonsters()` path created no beast. Diagnostic output
reports a total `max_per_gen` of zero, with family bounds 86,78 plus 50,50.
The monster family exists and has positive health, but a configured row alone
is not evidence of an available encounter. No production data was changed.

`obj/guild-beast-tests.log` records the failed availability check. The fixture
must continue to exercise production availability; increasing only the fixture
count would hide the missing player-facing event.

Current uncommitted kill-credit code adopts static-loader actors and hooks death
and respawn. This is insufficient for the intended event and is not release-ready.
No achievement inventory increment or completed milestone is justified.

## Original rules and conflicting editions

Official dedicated Guild Beast guide:
https://co.99.com/guide/guides/guildbeast.shtml

- Beast Guard is on the Guild Map at 81,81.
- The weekly winning guild leader pays 500,000 guild silver.
- It describes a daily 20:45 release, a 21:45 defeat deadline, public combat,
  one Dragon Ball per day, and leader/deputy fallback collection before midnight.

Official guild introduction:
https://co.99.com/guide/guides/guild1,5.shtml

- Confirms winning-leader funding at 500,000 silver.
- Describes the encounter from 21:00 to 22:00 instead.

These are conflicting published editions, not verified patch-6711 timing.
A Revora implementation must document its selected schedule explicitly rather
than presenting either time as binary-confirmed. The selected implementation
baseline is the later 21:00-22:00 description, in server-local time, with the
original public combat and daily single-reward limit. This is a documented
Revora scheduling choice, subject to owner correction.

## Required implementation and validation

- [ ] Resolve the latest completed weekly winner from durable Guild Season
  results, including restart, no-winner, disband and leadership-change cases.
- [ ] Route Beast Guard through the ordinary NPC service with range and saved
  leader authority checks. Discover the installed NPC identity before wiring it.
- [ ] Debit guild funds atomically with a unique daily summon entitlement;
  synchronize runtime funds without an independent setter write or duplicate fee.
- [ ] Spawn an event-owned family-7006 actor during the selected window using
  legal terrain. Keep the static count at zero; prevent generic timer respawn.
- [ ] Accept achievement credit only from the registered event life and actual
  eligible killer. Persist an accepted kill/result before marking rewards done.
- [ ] Deliver one daily Dragon Ball through a durable claim path; preserve the
  owner reward policy when specifying any additional Revora event rewards.
  Do not silently substitute generic random loot for the documented reward.
- [ ] Despawn at the deadline and atomically exclude kill and fallback rewards
  from paying the same day twice. Validate authorized leader/deputy collection,
  midnight expiry, reconnect/restart, overflow and database failures.
- [ ] Verify ordinary non-GM availability, immediate achievement projection,
  full reload, competing lethal callbacks and counterfeit spawn rejection.
- [ ] Update inventory and completed roadmap marks only after the production
  earning route passes. Keep native acceptance and deployment separate.

Existing seams: `Game/Features/GuildSeasonWar.cs` persists battle state and
keeper; `Game/ConquerStructures/Society/Guild.cs` exposes guild silver funds.
Neither an in-memory pole keeper nor a cached fund value alone is sufficient
payment authority. No Beast Guard handler was found by the bounded source
search for BeastGuard, Beast Guard, GuildBeast or family 7006 in `Npcs.cs`.

## Funding transaction progress

`GuildBeastFunding` now provides the isolated database reservation primitive.
It requires an owning transaction, locks the saved character then guild, checks
saved leader rank/name and the latest scheduled battle (not simply the latest
winning row), rejects expired ownership, reserves a unique server-local day and
debits 500,000 silver in that same transaction. Funding closes at 21:00.
UTC instants and date-only keys are passed separately to avoid timezone casts.

An isolated Debug/x86 build passes. The focused fixture passes 11 funding
assertions plus its installed-family baseline check (12 total), covering eight
concurrent attempts, duplicate-free debit, insufficient funds, failed debit
rollback/retry, saved leadership, newer empty results, ownership expiry and
reopening the database connection. This is not fresh-process event acceptance.
Logs: `obj/guild-beast-funding-build.log`, `obj/guild-beast-funding-tests.log`.
The maintained runner exposes `-GuildBeastFundingOnly`; this validation used the
existing isolated wrapper to avoid unrelated working-tree starter-pack changes.

The primitive is deliberately not initialized or called by production NPCs yet.
`Database/GuildTable.cs:SaveFunds` still writes the cached guild balance and
`Network/GamePackets/GuildCommand.cs` still has a legacy donation writer. These
must be reconciled with transaction ownership and committed projection before
funding is exposed. Passing reservation tests alone does not prove concurrent
live donations safe. No money was charged in production and no new player-facing
service or achievement producer is claimed by this batch.

### Donation writer reconciliation

The normal guild-command silver and CP donation branches now use
`GuildDonations`: owner/session/guild ownership, saved membership and donation
preimages, saved fund locking, personal debit and progress in the same
transaction, then committed client/guild projection. A CP donation no longer
writes cached silver. A silver donation uses the saved balance, so a prior
summon debit is preserved. Identical rapid repeats are suppressed for one second
per session; this is not a durable request ID or a reconnect replay guarantee.

The isolated build and 384-check guild suite pass, including 10 new checks for
ordinary member donations, cached-fund divergence, CP isolation, rapid repeats,
intentional later donations, busy guild ownership, invalid inputs/membership,
rollback on refused fund credit and full character reload. Evidence:
`obj/guild-donation-build.log`, `obj/guild-donation-tests.log`.

Remaining funding writers still need reconciliation: arsenal unlock/enchant
(`SaveFunds` callers), profile handling that assigns cumulative donation totals
to current guild funds, and advertisement deductions. Guild creation/loading
assignments are initialization, not live spending. These remaining writers mean
the Beast Guard purchase must still stay disconnected. This batch does not
complete the event, add achievement coverage, or authorize deployment.

### Profile and arsenal-unlock reconciliation

Guild profile requests now only project the player's counters. They no longer
assign cumulative donations to treasury balances or change offices on view.
The guide counter comes from guide donations instead of CP donations. Existing
explicit promotion paths remain separate. The profile serializer still uses its
existing offsets; native UI acceptance is pending.

Arsenal unlocks now acquire ordinary leader/session/guild ownership, validate
saved leader membership and current guild authority, lock the saved fund and
arsenal rows, debit the established 5/10/15/20-million price by purchased count,
and insert the slot in one transaction. Projection occurs after commit. Cached
funds cannot overwrite a summon debit. Duplicate slot requests and insufficient
funds do not charge; insert failure rolls back payment. The normal packet
route remains available to eligible ordinary guild leaders, with bounded
framing and slot checks before dispatch.

Build and the full 395-check guild suite pass. Eleven additional checks cover
profile non-mutation/counters/malformed requests and concurrent unlock replay,
insufficient funds, ordinary-member rejection, rollback/retry, the third-slot
price transition and the production arsenal reload. Logs:
`obj/guild-funds-build.log`, `obj/guild-funds-tests.log`.

Arsenal enhancement still calls the legacy absolute `SaveFunds` writer and
advertisement handlers still deduct cached balances. They remain prerequisites
before the funded daily encounter is exposed. No event producer count increase,
live deployment, native acceptance or full event completion is claimed.

### Remaining active fund-writer reconciliation

Enhancement now uses a per-request level instead of a globally shared packet
buffer. Saved leader/member/guild/slot authority, guild fee, slot enhancement
and guild enhancement total commit together. Updates include both guild and
slot identity; the old helper updated every guild's matching slot. Established
levels 1/2 and price 50,000 times level are retained; same/lower levels cannot
charge again. Projection respects the existing five enhancement / fifteen total
battle-power caps. Ordinary eligible guild leaders use the existing packet path.

The active 2225 advertisement handler only deducted cached silver and changed a
bulletin; it did not publish an advertisement. The other recruitment adapter's
packet dispatcher was commented out and its requirement fields overlap in the
legacy serializer. Both purchase adapters now report unavailable without debit.
This follows the repository requirement to keep unconfirmed protocol families
disabled globally; it is not a completed advertising service or an owner-only
service. Verified advertisement publishing remains separate work.

Source search now finds no active callers of absolute `GuildTable.SaveFunds`
or unscoped `ArsenalTable.UpdateEch`. Remaining guild fund assignments are
initialization/load, read-only snapshot copies and committed projections.
This reconciles the identified active writers for the summon integration.

Build and 402 guild checks pass, including concurrent enhancement replay,
other-guild slot isolation, invalid authority/levels, rollback/retry, production
arsenal reload and advertisement no-charge behavior. Logs:
`obj/guild-fund-writers-build.log`, `obj/guild-fund-writers-tests.log`.

Limit: the legacy enhancement's 30-day timestamp is still only an in-memory
field, with no durable expiry consumer. This batch does not claim repaired
expiry or full enhancement battle-power reconstruction after a fresh Host.
That pre-existing lifecycle gap is separate from fee isolation and remains
pending. The funded Guild Beast event still needs NPC access, owned spawning,
kill/timeout settlement and complete ordinary-player acceptance.

### Installed NPC and old handler correction

Read-only installed-catalog inspection identifies BeastGuard as NPC 8150,
map 1038, coordinates 56,56, mesh 8150, type 2. Keep this installed location;
the official guide's 81,81 is not the installed Revora location.

A numeric-ID search finds the old `Npcs.cs` case 8150. This supersedes the
previous bounded name-search result: a handler does exist. Its dialogue says
21:00-22:00, but its action only checks the first ten seconds of 21:00 and calls
`Spawnthis` with family 4211. The installed catalog identifies 4211 as
HollowBeast (80,000,000 HP, mesh 953), while 7006 is Guild Beast (65,535 HP,
mesh 279). It neither charges the documented fee nor settles fallback delivery.
Read-only evidence log: `obj/guild-beast-npc.log`. The new adapter must replace
that numeric case, not coexist with it as an alternate summon path.

### Durable daily encounter state

`GuildBeastRuns` adds paid-day binding to an exact runtime life/actor, a single
accepted-kill transition with achievement 10303 in the same transaction,
timeout/expiry transitions and a unique delivery marker. Rebinding after a
restart invalidates the previous actor's death callbacks. A failed achievement
write rolls back the killed state. Kills are accepted in [21:00,22:00); timeout
fallback may be claimed by the funded guild's saved current leader/deputy until
midnight. That entitlement follows the guild that paid for the day. Killer
rewards remain recoverable after midnight; a clock rollback cannot claim before
the saved settlement. These are explicit Revora recovery semantics.

Build and 24 focused funding/lifecycle checks pass. Evidence logs:
`obj/guild-beast-runs-build.log`, `obj/guild-beast-runs-tests.log`.
This is a transaction-primitive test, not an accepted monster fight or delivered
inventory reward. The caller must hold character/guild authority as needed,
validate the registered world actor and insert the reward item in the same
transaction as its delivery marker. The schema is not yet initialized by the
production event and no paid NPC is wired to it. Event-owned spawning, combat,
NPC purchase/claim, real inventory insertion/reload and fresh-Host tests remain.
Do not count the otherwise callable evidence method as a completed producer.

## Completed source integration

The earlier disconnected-prototype and missing-world notes are superseded by
[the implemented daily event](GUILD_BEAST_EVENT.md). Beast Guard funding,
ordinary Guild Director travel, timed family-7006 spawning, owned combat,
transactional inventory delivery and timeout fallback now have server-side
fixtures. Build, 428 guild checks and 41 player-defeat regressions pass. The
previous static-loader credit prototype was removed. The ordinary travel test
caught a blocked fixed landing tile; the implementation now chooses verified
walkable terrain beside the installed guard. Native/fresh-Host/deployment gates
remain pending; none of those gates is implied by this source milestone.
