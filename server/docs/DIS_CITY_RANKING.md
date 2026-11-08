# Dis City daily finish ranking

Baseline: `f3ab920`, 2026-09-15. M6.23 implements the next-day ranking path
for the current single-difficulty Revora dungeon. Native client acceptance,
difficulty variants and deployment remain separate unfinished work.

2026-09-21 source update: standings now partition by the admitted difficulty and
combat revision, with the same prize quantities in every bracket. Historical
rows migrate to Standard revision 1; claimed receipts are retained. See
[difficulty scope and validation](DIS_CITY_DIFFICULTY.md). Native acceptance and
deployment remain open.

## Evidence and chosen edition

The [official Dis City guide](https://co.99.com/guide/quests/monstercity.shtml)
describes rank prizes from Solar Saint on the following day for the ten fastest
heroes. Its exact reward quantities, tie policy and difficulty parameters are
not specified. The [2012 prize upgrade](https://co.99.com/guide/event/2012/prize_upgrade_9th.shtml)
describes older challenge rewards and a Dis City Box; it is not evidence for
the current four-stage edition's daily ranking amounts.

Revora uses existing, validated bound Sacred Refinery Packs (`3008058`):

| Place | Packs |
| --- | ---: |
| 1 | 3 |
| 2 | 2 |
| 3–10 | 1 |

These modest amounts are explicit Revora tuning. They are not presented as
verified original CO quantities. The existing pack-use route selects a Sacred
material and preserves its ownership and refinement behavior.

The guide's linked `SolarSaint.jpg` shows the NPC, not difficulty choices.
Read-only inspection of the installed `Questinfo.ini` four mission records
confirms the stage objectives and Monday/Wednesday/Friday admission, but does
not identify difficulty labels, exact stat multipliers or rank payouts. Do not
mark difficulty variants complete from that evidence. The reviewed image is
kept only under ignored `research/isolated/dis-city-evidence/`, source
`https://hw.99.com/uploads/allimg/co/SolarSaint.jpg`, SHA-256
`7FCFD596E33C6171B2280D148A264909B2F4157AC9358F2723604258814235AB`.

## Player behavior

Solar Saint in Twin City offers **The swiftest heroes**, showing yesterday's
first three finishers and the requesting player's top-ten place when present.
**Claim my rank prize** awards the oldest unclaimed earned prize. Ordinary
players use both options; no GM, account-name or owner allowlist applies.

The ranking day is the Pacific calendar day of the accepted Pluto defeat.
Duration runs from admission to that defeat and includes time spent away from
the dungeon. A full bag or delayed final victory collection does not worsen
the finish time. Finishes are ordered by exact stored elapsed ticks, then
finish timestamp, then character UID. The latter two rules provide deterministic
tie handling; the displayed seconds are rounded only for presentation.

The next calendar day opens the prize claim. Existing unclaimed prizes do not
expire. Full bags leave the complete prize pending. Missed days and a stopped
server do not require a live midnight timer: a board/claim request finalizes
the relevant closed day from saved finishes. Claim preparation processes at
most 16 previously unfinalized days belonging to that character per request;
older backlog can be continued with another request.

## Durable ownership

`DisCityQuest.TryDie` records a finish only for its owned, living summoned boss,
after all prior stage/token checks and the saved phase transition to victory.
The same transaction writes the death step and `revora_dis_finishes`. A finish
references the immutable `(run,step)` death receipt and has a unique run and
unique character/day. It does not reference the replaceable active-run row as
a foreign key, so a later weekly admission does not invalidate history.

The recorder requires the caller's character lock, canonical eligible player,
transaction and matching saved stage-four victory/death source. It uses a
day-specific PostgreSQL transaction advisory lock shared with settlement, then
resamples the acceptance clock after acquiring that lock. Midnight rollover
uses the new day; unstable clocks abort after three attempts. A finalized day
rejects late source insertion through this path. Failed recording leaves the
boss alive and the saved run before victory, allowing a reload and retry.

Settlement writes `revora_dis_rank_days` and its ordered ten-or-fewer prize rows
in one transaction. Empty closed days are valid; partial failure cannot leave
a finalized day without its intended winners. Finish names and rank reward
quantities are stored snapshots. No legacy completion row is backfilled into a
rank finish: old final-claim elapsed seconds do not prove the battle-end time.

A claim locks the character and its prize row, inserts every bound item and
stores the original item UIDs with the claim timestamp before commit. Cache and
client inventory updates follow commit. Failed writes preserve the complete
entitlement; projection failures fence the session for reload. Repeated requests
cannot claim the same prize twice, but can claim another older pending prize.
There is no new request-nonce or native ranking-packet claim.

The shared day locks use the dedicated integer namespace `67116475` and the
numeric `yyyyMMdd` date. Multi-day claim preparation acquires dates in ascending
order. No account credentials or private player data are logged by these paths.

## Validation and client stage

The isolated Debug/x86 build passes. Assembly SHA-256:
`1E262F69AEC6A46EA2CBDBB2B6B04E0F653937AAF83F0E88D2B80F9E00FA7D01`.

The Dis City suite passes 164 checks, including 22 additional ranking checks:
actual owned Pluto finish recording/rollback, frozen time across delayed reward
collection, Pacific midnight/DST cases, twelve synthetic finishers with ties,
concurrent settlement, the top-ten boundary and three reward quantities,
full bags, payout rollback/recovery, concurrent/replayed claims, item loading,
old unclaimed rewards, a day never settled while offline, settlement failure,
foreign/missing sources and an acceptance clock crossing midnight.

The twelve comparative standings and isolated boundary scenarios seed controlled
victory/death records and call the production recorder. They do not each claim
another complete dungeon traversal. The genuine four-stage traversal separately
proves that the actual owned boss handler is connected to the recorder.

The source audit remains 81/337 producer identities. This reward path is not a
new achievement identity. Three audit and 12 private-overlay tests pass.
The 228 EXP/progression/Horn checks and all 1,408 other quest/event regressions
also pass against this assembly. No unexpected ranking failures occurred in
those regression modes.

Private stage: `obj/chi-refill-validation/dis-city-ranking-overlay`.

| Output | SHA-256 |
| --- | --- |
| `ini/itemtype.dat` | `96f0596b854f2e3ff9df6007317c5272fb9d08329995c6dc3357d33addc86f1d` |
| `ini/Questinfo.ini` | `1cf153510ca3767b49345192c57e852e44abd122f27fd36bc9f993c84028f310` |

The private overlay adds the next-day rank amounts and retained claims to the
four quest records while preserving the eleven reviewed item tooltips. It is
unsigned and not installed or publicly released. Real client dialogue, inventory
presentation and multiplayer acceptance remain pending, together with inherited
deployment gates such as ambiguous historical mentor relationships.

Difficulty selection must define its combat/reward edition and fair ranking
partition before enabling additional difficulties. The present board contains
only the existing single combat profile; it does not mix unverified difficulty
levels or claim the full dungeon milestone is closed.

## Settlement and prize write protection (2026-09-20)

Review against `4c9c717` found two unchecked database outcomes. A suppressed
day insert was interpreted as an already settled day, and a partially suppressed
winner insert could commit a finalized day without all entitled prizes. The
shared quest item insertion helper also ignored its affected-row count, allowing
a claim receipt to commit without its full saved item reward.

Settlement now checks for an existing day explicitly under the same advisory
lock used by finish recording. A new day requires exactly one saved day record
and exactly as many prize records as its eligible top ten finishers. Empty days
remain valid. The shared `QuestInventory.Insert` requires one saved item; a
missing first or later item aborts the calling reward transaction. Nothing is
projected into inventory before the complete transaction commits.

The added ordinary-player fixtures suppress the day record, all winner records,
only second place, the first reward item, the third reward item and the claim
receipt. They check whole-transaction rollback, retained entitlement, service
reinitialization, concurrent settlement/claims and once-only recovery. Prize
binding is checked through the production item loader. Controlled finish rows
isolate these failure boundaries; the existing full four-stage journey still
tests the genuine Pluto producer. Service reinitialization is not a full Host
restart or native client acceptance.

The original build fails the new suppressed-day regression. The repaired
isolated build passes all 176 Dis City checks, 43 Labyrinth checks and 35 mail
checks. The latter two cover other callers of the shared item helper. Difficulty parameters remain
unresolved; this repair does not enable variants, rewrite historical settled
days or close the full Dis City milestone. No live deployment.

## Fresh completed-run next-day rank prize (2026-10-03)

The fresh character's real2041-03-11 Dis City completion supplies the finish row.
On a controlled next-day clock, Solar Saint settles that result and pays rank1:
three bound Sacred Refinery Packs3008058. No finish, placement, prize or item is
inserted by the fixture. This is a single-entrant private ranking, not a
competitive speed or difficulty result. A repeated claim grants nothing.

The ordinary item handler opens these and the character's other owned matching
refinery packs, verifying saved output ownership and binding. The exact prize
receipt, resulting items and nested career survive game/database restart, with
another rank claim refused. Both phases of the
[fresh Treasury batch](DIS_CITY_TREASURY.md#fresh-earned-key-and-reward-opening-2026-10-03)
pass. Natural day rollover, native board display and live deployment remain open.
