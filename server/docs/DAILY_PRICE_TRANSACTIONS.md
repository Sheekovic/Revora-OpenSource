# Everything Has a Price: durable daily quest

2026-09-21 update: new contracts include one bound level-1 Tortoise Breaker rune.
Existing and imported legacy contracts retain their original rewards. See the
[saved contract, capacity and transaction rules](DAILY_REWARD_POLICY.md).
Native acceptance and deployment remain pending.

Implemented from `3db3c01` on 2026-09-16. Ordinary eligible players can accept
any of the four existing hunts, use their list to travel, gather the materials,
return and receive the complete configured reward. Paid changes, paid
completion, lost-list replacement and reconnect recovery use durable quest
state. This milestone completes the existing Revora edition's server earning
path; it does not claim official numerical parity or genuine-client acceptance.

## Rules and evidence

The existing server's four material mappings were verified against installed
monster spawns and terrain in [Daily Hunt Materials](DAILY_HUNT_MATERIALS.md).
The default exclusive random bound is corrected from 4 to 5: the existing
Phoenix City assignment is now selectable alongside the other three. Rerolls
choose a different available assignment without an unbounded retry loop.

The [official guide](https://co.99.com/guide/quests/2012/Everything_Has_A_Price.shtml)
supports a level-60 daily hunt, a list with travel, changing the task, paid
completion and tradable materials. Its level-based assignments and reward
brackets differ from the custom server. The following defaults are retained
from the local server, except for enabling the fourth existing assignment:

| Rule | Revora behavior |
| --- | --- |
| Admission | Level 60; physical Daily Quest Envoy 79882 in Twin City |
| Offering | 20 Bandit's Blades, 20 Bloody Teeth, 25 Bat Blood or 25 Blade Scraps |
| First/next changes | 200, 400, 600, 800 CPs; at most four |
| Paid completion | 5,000 CPs plus 250 for each prior change |
| Reward | 2,500 CPs, 15 RVPs, 25 study points, one EXP Ball of direct experience and one Chi Token |
| Daily boundary | Pacific midnight with daylight-saving rules |

Reward currencies, study, Chi identity, change tariff, skip tariff, maximum
changes and pool size are recorded when the hunt is accepted. A later change
to the operator configuration cannot alter that accepted reward or tariff.
Direct EXP is calculated at claim level; a capped character still receives
the non-EXP bundle without changing saved EXP. Bound inputs keep the resulting
Chi Token bound. The source drop chance remains the configured 40 percent.

## Atomic state and ownership

`revora_price_runs` is keyed by character and day. It holds assignment,
required material/quantity, changes, fees, issued list identities, completion,
consumed item identities/quantities and the reward/EXP receipt. Mutation locks
the character's inventory and database owner row. Experience claims also use
the shared mentor lock. Bag identities and selected material metadata are
checked against persistence before consumption.

- Acceptance inserts its new daily record and required list together. A full
  bag does not strand a fresh accepted hunt without its list.
- Reroll commits the CP debit and replacement assignment together. A reviewed
  dialogue continuation is single-use and expires after two minutes.
- Completion commits material quantities or the skip fee, optional list
  consumption, all currencies, Chi, progression, skills, Arena effects, mentor
  credit, recorded rebirth recovery and daily completion in one transaction.
- Item and point updates are sent only after commit. A persistence error
  retains the uncommitted offering/payment and fences the session for reload.
- Lost-list reissue cannot accept a new hunt or issue multiple lists from
  repeated clicks. Old lists alone grant neither a task nor a reward.
- List travel requires the exact owned inventory object, database ownership,
  current day/assignment/change version, a fresh single-use continuation and
  a supported physical map with a legal landing. Existing event teleport
  restrictions still run in `Entity.Teleport`.

The latest durable day prevents clock rollback and legacy mirror resets from
reopening completed rewards. Returning players and NPC menus project the
ledger into the existing quest flags and 90004 tracker. At first adoption,
saved legacy active/completed state is imported only when its saved reset
date matches the current legacy server date. Arbitrary RAM flags cannot prove
an old reward. Unfinished older-day hunts can be replaced the next day.

The disabled-by-default legacy daily-reset option now checks its configuration
and physical NPC proximity before charging. When enabled it refreshes the new
durable quests after changing legacy mirrors. It does not reset the Price,
Magnolia or Release Souls ledgers. Its remaining legacy-task transaction is
outside this completed hunt contract.

## Validation

The isolated build passes. The focused suite has 511 checks: 392 shared
first-rebirth prerequisites and 119 Price Hunt checks. All four routes use the
actual NPC service, production reconnect, owned item use, a sealed native list
dialogue packet, installed monster-family deaths and native floor pickup to
earn the entire offering. A supplied owned Twin City Gate returns the player;
server walking reaches the Envoy for turn-in. Travel-item purchase itself is
not asserted by these fixtures. Monster damage and random rolls are controlled;
the route is not a real-client combat/pacing capture.

Other cases cover exact minimum level, free/paid completion, repeated and
concurrent offers, autosave races, daily rollover, backward time, legacy
imports, frozen tariffs/rewards, exact stacks and bound inputs, nullable and
stale balances, full bags, capped progression and failed writes at every
compound-reward boundary, followed by production reload/retry. Stale,
replaced, missing, expired or database-unowned lists cannot travel. Truncated,
oversized, trailing-byte or invalid-input-length list frames are rejected,
and a valid continuation still succeeds afterward.

Before the final scoped NPC frame-bounds guard, 2,338 gameplay checks passed
(Price 506, hunt loot 475, Magnolia 466, Souls 35, quest packets 9, travel 105,
experience 452, progression 74, Potency/daily bosses/Extraction 216). The final
build is checked by Price 511 plus the affected Magnolia, Souls, quest-packet
and travel regressions: 1,126 checks. The three achievement-audit tests pass;
the producer upper bound remains 219/337 and is not per-character evidence.
Final harness SHA-256:
`7C15D7A25ECC9965B778DFB062DC4960BDC33C6D9DC95DA1A9BEAF1F713B385D`.
Logs are ignored under `obj/chi-refill-validation/price-*.log`. Only disposable
`revora_potency_validation` data is written;
the installed catalogs/spawns are read-only evidence. No live restart, client
patch or deployment was performed.

## Remaining work

Rare Matters is now covered by [its completed transaction and mining routes](RARE_MATERIALS_TRANSACTIONS.md). Soul Ball compound rewards remain open. Official hunt
assignment brackets and exact historical reward/fee parity require their
separate edition review; the custom economy above is intentional in this
transaction batch. Native 6711 NPC text, list travel, inventory and quest-panel
acceptance remain deployment gates. No new wardrobe achievement is invented
for completing this daily hunt.

## Valuable reward upgrade (2026-09-20)

New contracts freeze `valuable_reward=true`; existing and imported contracts
remain false. A new completion, including the existing paid option, adds one
unbound P7 soul selected uniformly from the loaded P7 catalog. `soul_uid` is
saved with the completion, Chi Token, material consumption, currencies and EXP
in the same transaction. It requires two reward slots after actual removals.
Old contracts retain their previous one-item reward. NPC reward text follows
the frozen contract. Daily limits, fees and other rewards remain unchanged.

Isolated build and 547 checks pass. Coverage includes full earned routes,
concurrent paid claims, rollback with no saved soul, reload/receipt identity,
full-bag retry, old contracts and repeated initialization after claims.
Log: `obj/chi-refill-validation/price-p7-reward.log` (ignored).
Live Host/native-client acceptance has not been performed for this upgrade.


## Retained hunt through separate processes (2026-10-01)

The Phoenix City assignment now has a composed-process earning/recovery route
using retained ordinary character4294908702, preserving its earlier event items
and purse. The main320-career character is not changed or credited by this route.

- NPC79882 accepts a new daily contract. The owned list and sealed2032 dialogue
  reach physical map1011. Twelve loaded Bandit/BanditL33 deaths and ordinary
  sealed1101 pickups supply twelve Blade Scraps, then the game exits abruptly.
- Fresh game/database startup retains the exact list, twelve item identities,
  pickup receipts and unfinished contract. Thirteen further deaths/pickups finish
  the25-material offering; another abrupt exit leaves the reward unclaimed.
- A third process verifies the exact saved offering. The installed physical portal
  returns the character to Twin City. Ordinary Envoy turn-in consumes list and
  offering, awards2,500CPs,15RVPs,25Study Points, one Chi Token and one bound level1
  Tortoise Breaker rune. There is no skip fee, P7 item or soul receipt.
- Cold claimed-state reload and repeated free/paid completion requests retain
  the exact saved snapshot without another reward or charge.

`PriceCareer` in `tools/CheckCandidateCareerRites.cs` owns phases
`career-price-start`, `career-price-hunt`, `career-price-claim`, and
`career-price-reload`. It checkpoints before the initial contract and after each
successful phase; it does not insert offering materials or completion receipts.
The assertions retain full saved inventory rows, purse/position/EXP, Study Points,
contract and pickup receipts. Duplicate floor pickup packets grant no second item.

Calendar, assignment choice, successful drop rolls, lethal damage and approach
coordinates are controlled. This does not establish natural combat/drop pacing,
other assignments' composed-process behavior or native acceptance. Their earlier
four-route handler tests retain their stated scope. Ground persistence itself is
not changed: only already-picked-up materials are recovered here. No production
source change, live restart or deployment is part of this batch.

Private executable SHA-256:
`61DB4C2F5EFE59F1F4B061AACC9D6AB9ED983EC8F05F64EB5AA4ABA59E715D46`.
All four phases pass. Phase logs are retained under the private candidate
directory, outside Git.

## Fresh career route1 with unforced assignment and drops (2026-10-03)

The production-created ordinary career now independently earns route1's20
materials through54 calculated kills, using the current quest clock and normal
assignment/drop RNG. Separate unclaimed/paid restarts, duplicate pickup refusal,
stale-menu refusal and exact once-only daily payout pass. The payout includes a
bound rune and no P7. This extends the older controlled-roll evidence for route1;
it does not newly verify all four assignments or native pacing.
[Checkpoint, rewards and fixture limits](CAREER_STARTING_BOUNDARY.md#fresh-daily-price-hunt-with-unforced-drops-2026-10-03).
