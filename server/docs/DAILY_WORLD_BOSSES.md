# Daily world bosses

Four public hunts run each server-local day. The current Windows server uses
Pacific time, including daylight saving time. Each slot independently chooses
one minute from 00 through 59 and stores it in PostgreSQL. The six-hour slots
therefore have actual spawn gaps of approximately five to seven hours.

| Window | Boss | Map | Hunting center |
| --- | --- | --- | --- |
| 03:xx | Moonfang King | Twin City outskirts (1002) | 438,664 |
| 09:xx | Dawnfire King | Phoenix Castle outskirts (1011) | 748,491 |
| 15:xx | Sunscar Tyrant | Ape Mountain (1020) | 616,507 |
| 21:xx | Nightveil King | Desert (1000) | 400,500 |

All four have 100,000,000 HP, level 140, 7,000 defense and a two-second
attack interval. Their base hit on a player is 6,500–7,500; later combat
effects can modify the final result. Their appearances reuse existing 6711
resources. Spawn placement uses a free walkable tile near the hunting center,
at least 12 tiles from NPCs and five from current players. The world message
gives actual coordinates and the deadline. Normal monster pursuit applies.

An undefeated boss disappears two hours after its scheduled arrival. At
most one managed boss can exist. Startup resumes an unfinished hunt only
while its original deadline remains open; missed hunts expire. HP and
position checkpoint every 30 seconds, so an abrupt crash can restore up to
30 seconds of damage. Defeated bosses never return for that day's slot.

The ordinary authenticated player landing the killing blow receives exactly
1,000,000 CPs, 50,000 RVPs and 10,000,000 gold. No GM status is required.
The durable kill marker and four loot choices commit together; the currency
payment and paid marker commit together separately. An unpaid bounty retries
while its killer is online and the inventory lock is available. Overflow
defers payment. An ambiguous payment failure fences the session for reload.

Four ground drops are selected uniformly, with replacement, from the loaded
P7 purification soul catalog (33 eligible items in this installation).
Repeated soul types are possible. Loot remains for 120 seconds and follows
the normal initial owner/team protection (15 seconds). Each successful
pickup commits the claim and inventory row atomically. Restart restores
unclaimed, unexpired loot with the same choices; duplicate clicks cannot
mint a second item. Legacy generic drops and bonus CP rewards are suppressed
for these four reserved families (9912000–9912003).

Additive tables: `revora_daily_boss_runs` and `revora_daily_boss_loot`.
Operator status: `database/daily-boss-status.json` with UTC plans, server
timezone, state (0 scheduled, 1 active, 2 defeated, 3 expired), active UID/HP.
Do not reset these tables to restart the server or roll back an assembly.

## Client HP correction and validation

Patch 1.0.13 corrects the four daily and five Boss Rush profile headers from
1.0.12: the native parser requires `~` to encode spaces. Literal spaces in
1.0.12 prevented exact-name lookup. Displayed server names remain unchanged.
Boss Rush HP remains 1M/3M/6M/9M/12M respectively. All nine corrected aliases
resolve through the actual DX9 GameData loader with Boss=1 and the right HP.
The previously released HorrifyingSpecter alias remains unchanged at 5M.
The server now sends actual HP in both spawn field 23 and HP attributes;
the old 10,000 normalization caused the owner's `10000/5000000` display.
See BOSS_HUD_6711.md and PROTOCOL_RESEARCH.md for approved-client evidence.
Full client exit and launcher relaunch are required after patch installation.

`tests/PotencyHunt/Verify-PotencyHunt.ps1` passes 210 checks against an isolated
disposable PostgreSQL schema and actual cached terrain. These cover HP packet
round trips at full/half/1/0 HP for HorrifyingSpecter, Boss Rush and all four
100M daily bosses; all four exact requested placements; damage bounds; persisted random schedules;
HP recovery; exact once-only ordinary-player payment; lock contention; kill
transaction rollback; four real P7 ground drops; restart restoration without
rerolls; protected and duplicate pickups; and both expiry paths. The suite
also preserves the existing Potency and retired-boss-reward checks.
No fixture starts a listener or changes production player rows.

Client preparation validates the approved DX9 hash, exact catalog preimage,
encryption round trip, preservation of every byte except nine section headers,
and archive contents/CRC. Patch 1.0.13 archive SHA256:
`ec63d85096e3f2144f624ee69777153c84b659f62a965f5c2f4b19b852851776`.
Staging/backups/manifests are ignored under
`obj/chi-refill-validation/boss-hud-space`. `Verify-BossProfiles.ps1` reproduces
the nine failing lookups with 1.0.12 and verifies all nine with 1.0.13 through
the native loader. HorrifyingSpecter resolves correctly in both. This verifies
profile eligibility and maximum HP; rendered in-game bars still need visual confirmation.

## Reward transaction correction (M6.49)

All four loot inserts must now report one saved row before a kill commits.
A silently suppressed insert rolls back the earlier choices and leaves the boss
available for another finishing blow. A soul pickup likewise requires a saved
inventory row; failure retains the unclaimed floor reward and fences the session.
Bounty payment checks the saved RVP balance alongside CPs and the locked silver
preimage. An unexpected saved RVP credit survives, with the bounty left unpaid
until a healthy session can retry.

The staged build passes 216 Potency/daily boss/Extraction checks, including three
new database fault cases. The third-loot suppression test failed against the
previous implementation and passes with the correction. Tests exercise the real
failure/disconnect path without starting timers; retry uses explicitly reset
fixture sessions and synchronized balances, not a claimed native reconnect.
Existing exact-once payout and pickup assertions verify the subsequent retries.

The timestamp audit found no daily-boss timezone defect: these tables store
UTC clock values in PostgreSQL `timestamp without time zone` columns. Their
reader intentionally assigns UTC to those values. This differs from the new
Terato `timestamptz` fields, whose offset-aware reads convert to UTC.

Staged server SHA-256:
`6D60BF65A101864FDE4388C78C5F70242B359C527FA486EFA1E2C0CC9FC9D971`.
No live process restart, deployment, client change or database migration occurred.


## Bound P7 correction (2026-09-27)

The shared daily-boss loot path previously created unbound souls and omitted
`bound` from its inventory INSERT. New and restored unclaimed ground souls now
use `Bound=true`; pickup persists `bound=1` in the same transaction as the loot
claim. The kill announcement identifies the four bound souls. Already-owned
items and completed claims are not rewritten. The bounty, four-draw pool,
protection and expiry rules are unchanged.

- [x] Isolated build with the committed versions of unrelated owner work passes
  with existing legacy warnings (`obj/daily-bound-soul-build.log`).
- [x] `tools/Verify-ArenaProcess.ps1 -DailySoulsOnly` passes two actual game
  processes around a database restart: generate four bound P7 drops, pick up one
  twice, reload its exact saved UID/type/binding, restore the other three, and
  pick each up twice. Exactly four inventory items and four claims remain;
  each saved item has `bound=1`. The ordinary killer's original currency bounty
  is also checked.

Private game SHA-256:
`A40A9692DAD1C63CEF1C6F7E53A4F77F02AA98CDFDEA413FB7E352F7A8E67403`.
Evidence: `obj/daily-bound-soul-process.log`, helper
`tools/CheckCandidateDailySoulLifecycle.cs`. The fixture uses Moonfang through the
shared four-boss implementation, controls the event clock/proximity and invokes
the production death handler directly; it does not claim authenticated combat,
100M damage, all four separate fights or native presentation. The owned clone
was removed after success. Live deployment remains pending.

## Full calculated combat on the fresh ordinary character (2026-10-03)

The new private fixture completes Moonfang's full100M HP in7,220 normal melee
attacks and survives1,804 boss attacks (12,613,054 total damage;6,433 minimum HP).
The character retains its earned equipment and purchases1,824 stack-limited
7,000-HP medicines at1,000 silver each;1,801 are consumed. Inventory provisioning
uses normal warehouse and shop requests. No health, money, gear or lethal damage
is injected.

The controlled clock accounts for500ms melee intervals,2-second boss intervals
and18 modeled90-second resupply round trips after initial provisioning:87m11s,
within the two-hour hunt. Positions and elapsed time are controlled. Autonomous
pursuit, actual navigation and real-time client acceptance are not established.

The exact bounty and four bound P7 items survive a cold game/database restart.
Each floor pickup is repeated without duplication; payout replay preserves the
full nested snapshot. An initial test-only reflection error occurred after the
kill was saved. A dedicated loot phase preserved that kill and its payout rather
than repeating combat. The final check verifies four exact saved item UIDs.

Evidence: [numeric outcome and log hashes](research/FRESH_DAILY_BOSS_COMBAT.json),
`tools/CheckCandidateFreshDailyBoss.cs`, private phases
`career-fresh-arena-classes-funding-prepare`, `-combat`, `-loot`, `-reload`.
The loot phase is for that interrupted inspection; successful new combat can
collect directly. No live deployment or native acceptance occurred.
