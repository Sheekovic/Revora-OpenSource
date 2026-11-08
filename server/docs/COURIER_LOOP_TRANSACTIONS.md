# Courier Loop earning and atomic completion

2026-09-21. Server earning and reward persistence are implemented and tested.
Genuine 6711 display/interaction, purification use and live deployment remain
pending. This is the existing Revora quest, not a claim of retail quest parity.

Cerys is at Twin City 1002 (319,303). Ordinary players report her satchel to the
City Commander (442,339), hunt 20 Bandits in **Phoenix Castle 1011**, obtain
PurseClerk's approval at (418,330), then return to Cerys. Her travel options
remain available for those stages. The fixture confirms installed Bandit spawn
rows in Phoenix Castle and exercises NPC handlers and actual `Entity.Die`
notifications. The old FEATURES reference to Bird Island was incorrect and is
now corrected; no monster/map relocation was made.

Each stage requires its registered nearby NPC, a canonical living player and no
active trade/booth. Kill credit requires a nearby registered wilderness monster
with matching installed family metadata. It is saved after every credited death,
not just at the last Bandit. Each corpse counts once; normal revival opens a new
creditable monster life. Acceptance and rebirth reset hold the variable lock
through their complete progress save.

## One claim transaction

The owner inventory lock serializes payment against inventory and character
saves. The variable lock preserves a coherent legacy progress snapshot. One
checked database transaction writes:

- One **bound P7 soul** from the existing class-oriented catalog selection.
- The configured purse: by default 8,000 CPs, 80,000 silver and 200 RVPs.
- The original level-120 puff when needed, including its stats and EXP reset.
- The original VIP policy: VIP 6 for seven days; preserve higher VIP and lifetime
  VIP 6, retain a longer remaining duration, and keep the existing display-days
  behavior. Players already at/above the puff keep their level and EXP.
- Completed legacy progress and a unique `(uid, life)` receipt in
  `revora_courier_claims`, including exact item UID and paid values.

Failed, suppressed or ambiguous writes do not reopen a partly paid reward.
The session requires reconnect after a failure. Client items, currencies, level
and VIP are projected only after commit. Existing paid completion bits stay paid;
receipts repair stale completion flags at the next quest NPC interaction. No
existing issued item is rebound. Stages above two keep the old capped life bit.

## VIP persistence prerequisite

`VipState` retains the actual loaded VIP level/days/display-days separately from
pending login minimum-level and daily-expiry changes. Claims, autosaves and
immediate VIP level writes compare that durable state and reject conflicting
external writes. Both day setters use the owner lock. Committed reward projection
does not invoke the old independent VIP database update.

The installed schema stores `vipdays` as **text**, while the other VIP fields are
small integers. A parameterized numeric-text comparison handles that legacy
column, including NULL, without a live column migration. Malformed or out-of-range
stored values fail validation. This does not redesign VIP expiry or its display
counter, nor authorize deployment of the changed login/save paths without the
usual acceptance gate.

## Validation and limits

The isolated build and `Verify-PotencyHunt.ps1 -CourierLoopOnly` pass **531 checks**,
including the existing 403-check first-rebirth prerequisites. Courier checks cover:

- The actual Commander ? 20 Bandit deaths ? Clerk ? Cerys route for three
  configured rebirth profiles, mid-hunt and post-Clerk reconnects, concurrent
  claims, a separate prepared autosave-race fixture, exact bound soul UID and complete purse/profile/VIP reload.
  Earned-claim reload assertions do not depend on a later character save.
- Suppressed purse, progression, VIP, item, progress and final receipt writes;
  rollback of the whole reward, fresh reload and a single successful retry.
  These failure cases prepare completed counters separately from earning tests.
- All seven stale purse/profile/VIP fields, NULL stored VIP, unflushed expiry,
  lifetime/higher/longer VIP, full/exact bag capacity, currency overflow, old paid
  progress, receipt repair, out-of-order stages, remote NPCs, invalid kill sources,
  corpse replay, revival, rebirth reset and stale immediate-VIP/autosave writers.

The tests use ordinary non-GM accounts. The three configured rebirth profiles do
not newly prove both rebirth rites on one character. Native client presentation
and actual class-specific purification compatibility remain separate checks.
The 510-check Soul Trial regression suite also passes with the new VIP tracking.
Logs are ignored under `obj/chi-refill-validation/courier-loop-atomic.log` and
`soul-trial-after-courier.log`. No live schema migration, restart or client patch
was performed. Catalog verification remains 29/29, and the achievement-producer
audit remains 240/337 IDs present with 97 missing; neither count is goal completion.

Known client-content follow-up: the installed `Questinfo.ini` mission 90002 still
points its Bandit navigation at map 1015 (458,527) and names Bird Island in the
content. Its prize says P7 soul without the new binding detail. The Commander
navigation ID 91001 is correct. A [private composed correction](STARTER_JOURNAL_REVIEW.md) is now prepared and
passes 17 journal tests plus hash-gated composition. Native review and release
remain pending; neither the active client nor its journal was edited. NPC dialogue already names the correct Phoenix Castle route.

Reload-test follow-up: earned-route concurrent claims now run without autosaves;
the latter are exercised in a separate prepared completion fixture. This ensures
the earned fresh reload cannot be made to pass by an autosave after the claim.
The revised suite passes 531 checks (`courier-reload-separation.log`, ignored).


## Real socket earning acceptance (2026-09-27)

Cerys's Phoenix travel destination is corrected from blocked130,250 to112,272.
The loaded terrain shows a clear landing connected to the actual Bandit fields.
The ordinary private character completed20 real combat kills, clerk approval and
turn-in through Account/Game TCP. Exact8000CP/80000silver/200RVP and level120
updates plus one bound P7 CraneRing were observed; SQL confirmed the same permanent
item and single receipt. Two further Host/database restarts retained the receipt
and reward; duplicate turn-in was refused. No quest progress or reward was seeded.
[Detailed evidence](ROADMAP_STARTUP_ACCEPTANCE.md#actual-courier-earning-and-saved-reward-2026-09-27).
Abrupt interruption, native acceptance and live deployment remain pending.

## Fresh earned life2 route (2026-10-03)

The production-created Warrior now completes the Commander/Bandit/PurseClerk
route with calculated combat and reloads before claiming. The existing level120
puff, purse, bound P7 and seven-day VIP6 survive a separate restart with no repeat
payout. [Exact evidence and limits](CAREER_STARTING_BOUNDARY.md#fresh-courier-quest-and-earned-level120-reward-2026-10-03).
No native or natural-expiry gate is closed by this private handler journey.
