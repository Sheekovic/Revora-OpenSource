# Soul Ball: durable wilderness earning and complete rewards

2026-09-21: new cycles now include a bound level-1 Tortoise Breaker rune;
existing and imported legacy cycles retain their original rewards. No daily
P7 is granted. See [current contract/capacity rules](DAILY_REWARD_POLICY.md).
Native acceptance/deployment remain pending.

Initial transaction implementation: `bd13166`, from `c97aca2` on 2026-09-16.
The [grade and Terato follow-up](SOUL_BALL_GRADES_AND_TERATO.md) supersedes the
initial random issuance and adds a registered boss source. `DailySoulBall` now owns the existing
Revora service's issuance, saved spirit count, paid completion, lost-bead
replacement and complete reward transaction. Ordinary authenticated characters
at level 100 or above can use it through the existing Daily Quest Envoy and
owned bead. This is a server implementation, not a live deployment or a claim
that every official Spirit Bead rule has been restored.

## Evidence and edition limits

The [official Spirit Bead guide](https://co.99.com/guide/quest/spirit-bead.shtml),
checked on 2026-09-16, confirms the daily hunt, five grades, right-click reward
and CP alternatives. Its required counts are 2,500/2,000/1,500/1,000/500. Normal
monsters supply 1/2/3/4 spirits across the level boundaries 70, 100 and 120.
The follow-up restores progression from the first Normal bead to better
grades and the registered Terato bonus. The guide's EXP, blessing and study
rewards differ from Revora's current economy. Other leader/boss bonuses and
remaining edition differences are tracked separately; the NPC does not promise
unimplemented bonuses.

| Rule | Current Revora service |
| --- | --- |
| Eligibility | Ordinary level-100+ character, current loaded session |
| Free bead | Normal first; completed hunts advance through Refined, Unique, Elite and Super |
| Super bead | 800 CPs; a separate issuance choice |
| Direct completion | 5,000 CPs, with or without an issued bead |
| Every reward | 2,500 CPs, 15 RVPs, one EXP Ball of direct experience, one Chi Token |
| Study | Normal 15, Refined 20, Unique 25, Elite 40, Super 80; direct completion 40 |
| Blessing | First four grades 24 hours; Super/direct completion 48 hours |
| Daily boundary | Pacific midnight; unfinished beads carry over |
| Replacement | Free, same grade and saved progress, only when the original item no longer exists |

The initial transaction pass exposed the formerly unreachable Elite branch.
The follow-up replaces random issuance with completed-hunt progression; the
old random-bound setting remains only for configuration compatibility. New beads are bound, and a bound consumed bead produces a
bound Chi Token. Legacy unbound beads remain usable. This change does not add
an achievement or infer an unsupported mission identifier.

## Transaction and lifecycle ownership

`revora_soul_ball_runs` records acceptance and completion days separately,
exact bead UID/type, spirit requirement/count, frozen reward and payment
terms, issue/completion CP charges, replacement UIDs and final reward receipt.
One active record per character and owner-row locks serialize operations.

- NPC actions require the physical Envoy, normal eligibility and a reviewed,
  single-use two-minute continuation. Repeated paid requests cannot charge
  twice. Active quests retain their saved tariffs and reward parameters.
- Issuance commits its CP deduction, exact inventory item and run together.
  A full bag cannot incur a charge.
- A full bead is redeemed by its actual inventory object, exact durable
  ownership and saved quest UID. A copied object or a different bead cannot
  authorize the reward. Locked, suspicious, expiring and stacked beads are
  rejected; a legacy zero-stack singleton is accepted as one item.
- Completion atomically consumes the bead or CP fee and persists CPs, RVPs,
  study, Chi Token, blessing seconds, progression, skill/Arena effects,
  mentor EXP, recorded rebirth recovery and completion receipt. Generic quest
  blessing does not generate Praying Stone mentor contribution.
- A consumed bead frees its slot for Chi. At the character level cap, all
  non-EXP rewards remain available. Overflow or curse checks preserve the
  whole offering. Currency and blessing preimages reject stale sessions.
- Item and balance notifications follow commit. A suppressed or failed write
  rolls back the entire exchange and fences the session until reconnect.
- A destroyed bead can be replaced without losing spirits or paying again.
  The original UID must be absent from the entire item table, including
  storage and other owners. Replacement insertion and the audited UID change
  commit together. An old object cannot redeem the new record.
- The latest completion date prevents daily-reset and clock-rollback replay.
  A carried-over bead uses the day it is redeemed, so it cannot also earn a
  second reward that day. The daily menu has its own ledger-derived status.

## Spirit earning and legacy coexistence

The production monster-death path calls the source handler before loot. It
requires a canonical dead monster and real killer on the same physical map,
an installed matching family/name/level, a valid owned bead and a current
ordinary-player session. Guards, revivers, companions, adventurers, bosses,
unknown templates and event instances cannot supply this ordinary source.
The follow-up adds Terato through its separate owned victory transaction,
rather than weakening these wilderness checks.

The currently admitted wilderness map IDs are 1002, 1011, 1020, 1000, 1015,
1001, 1013, 1014, 1021, 1022, 1210 and 1211. Normal rewards are 1 below level
70, 2 at 70-99, 3 at 100-119 and 4 from 120 upward; the exact level-70 bug is
fixed. Counts stop at the saved requirement. Each qualifying death updates
the durable count before reporting progress. A per-object life guard rejects
concurrent corpse replay and artificial death-timestamp changes. The actual
map respawn path resets that guard after restoring a living monster.

The old shared `spiritBeadsCount` remains the Anniversary counter. Daily kills
no longer increment it, and daily rewards do not read it after adoption.
For compatibility, one unambiguous, valid owned legacy daily bead can import
the persisted shared count once, capped to its requirement. A current saved
legacy completion imports as already completed. Mixed daily/Anniversary
holdings do not import progress. Historical ownership of that shared counter
cannot be reconstructed beyond the available saved state.

The legacy completion boolean still closes the Anniversary service; daily
refresh never clears it. Daily status is projected separately to avoid
reopening Anniversary rewards or showing yesterday's daily record as done.
The Anniversary issuance and full reward transaction themselves remain legacy
work and are not claimed as completed here.

## Validation

For the initial `bd13166` milestone, the isolated server build passed and the
focused fixture had 524 checks:
392 shared rebirth/material prerequisites and 132 Soul Ball checks. It covers
all five exact default requirements/reward grades, issue and claim concurrency,
nullable/stale balances, lost-bead recovery, storage duplication rejection,
ordinary reconnect, day rollover, source identity, corpse replay, respawn,
legacy adoption, level boundaries and failed writes across every reward layer.

The full earned Super route starts at the Envoy, pays for its bead, uses the
ordinary owned hunt list to reach Bird Island, kills 250 canonical level-97
monsters through `Entity.Die`, reconnects at 250 spirits, reaches the default
500 requirement and redeems all rewards before another production reload.
Combat damage/time are controlled by the fixture. Other grade reward tests
use explicit saved legacy progress; they do not claim four additional full
live hunts.

Before the final Anniversary-display correction, 517 focused plus 2,634
neighboring gameplay checks passed. After that correction and added mentor,
nullable-balance and ownership assertions, the final affected suites passed:
524 Soul Ball, 511 Price Hunt, 35 Release Souls and 9 quest-packet checks
(1,079 total), plus three producer-audit tests. The producer inventory remains
219 present / 118 absent; this is not proof of every character's earning route.

Initial milestone harness SHA-256:
`424AD729FB1B07D7F69EE57154C8578122EFA3DEC6CEAC793EBACC2751EC375B`.
Logs are ignored under `obj/chi-refill-validation/soul-*.log`.

Remaining work after the follow-up: original numerical/transition parity,
other leader/boss bonuses and further hunting locations, full Anniversary atomicity,
native 6711 UI/quest journey acceptance, and live restart/deployment validation.
No client artifacts, production database mutations or live binary deployment
are included in this milestone.

## Follow-up: Anniversary isolation

[Anniversary transaction isolation](ANNIVERSARY_TRANSACTIONS.md) now replaces the
legacy counter and partial reward writes with a separate owned ledger. The shared
daily allowance is enforced across both ledgers. New Anniversary admission is
not claimed complete: its installed NPC is absent and presentation/campaign
questions remain explicit.

## Valuable reward policy (2026-09-20)

New Soul Ball contracts freeze `valuable_reward=true` and add one unbound P7
soul at completion. All grades and the existing paid completion option receive
the same additional soul. Existing saved/imported contracts remain false; grade
progression, blessing, fees and other rewards retain their existing rules.
The soul is uniformly selected from the loaded catalog's P7 family and its UID
is written to `soul_uid` in the same transaction as completion, Chi Token,
bead consumption, currencies, EXP, blessing and Study Points. Capacity accounts
for both reward items after consuming the bead. Reward dialogue follows the
saved contract. Paid completion without a bead needs two free slots.

Validation log: `obj/chi-refill-validation/soul-ball-p7.log` (ignored).
Live Host/native-client acceptance remains pending.

Isolated build and 610 Soul Ball checks pass. Added checks verify the owned
Terato-earned soul, paid/concurrent completion, persisted UID after reload,
insufficient room for two gifts, rollback on a suppressed P7 insert and retry.
Existing fixtures preserve imported contracts, grade progression and blessing.

## Retained Normal bead and saved payout (2026-10-01)

The retained ordinary career takes its free Normal bead through Daily Quest
Envoy 79882. The earlier Spook kill occurred without a bead and is not credited
retroactively. Installed Bird Island portals and 1,250 loaded Birdman kills
through normal ranged attacks fill all 2,500 spirits. Approach coordinates,
attack cooldown and respawn callback time are controlled; no material, spirit,
damage or kill credit is injected. This does not verify native combat or timing.

Capacity is made through ordinary warehouse/trade handlers: owned spare weapon
59992607 moves to the existing peer, and the earned nontradable Exorcism Token
is deposited in its vacated warehouse slot. No reward is discarded. The full
bead and saved prior quest state survive abrupt process exit and restart.

Using the bead commits 2,500 CPs, 15 RVPs, 15 Study Points, 86,400 seconds of
Heaven Blessing, bound Chi Token 60508310/type729304 and bound rune
60508311/type4030101. Bead60506982 is consumed. No daily P7, issue charge or skip
fee is present. Earlier bag UIDs survive; capped experience remains unchanged.

The first claim fixture incorrectly checked BlessTime rather than HeavenBlessing
and failed after the transaction committed. The recovery validates the exact
inspected purse, study, blessing, unchanged experience, bag count, reward UIDs,
binding and completed contract; it never pays again. The original unpaid
snapshot is retained in `unpaid_snapshot`. The original claim phase is therefore
not presented as an uninterrupted passing run.

Two earlier setup failures assumed a Market return portal and a tradable
Exorcism Token. Corrected navigation uses Market NPC45 and ordinary storage;
no completion or earning state was reset. A later comparison also rejected
normal NPC approach coordinates. Recovery compares the full nested snapshot
except the main character's map/x/y fields, requires Twin City, then saves the
normal Envoy position before exact replay comparisons. All other snapshot fields
remain compared; this location allowance is fixture-specific, not gameplay.

`career-bead-hunt` passes; corrected saved-claim recovery and
`career-bead-reload` pass with repeated issuance refused and no extra reward.
Latest checkpoint: `revora_career_bead_probe` done=true / `CareerBeadSnapshot`.
Bag40. Candidate SHA-256:
`F5683ADB48B1D7015F0B998AA48F569431955CAA298FD506FE0BA78F35F93B96`.
Higher-grade rotation, boss combinations and native acceptance are outside this
batch. No production code, live restart, deployment or client release changed.

## Fresh paid Super bead filled through calculated combat (2026-10-03)

The production-created career buys its Super bead for800 earned CPs and fills
500 spirits through250 calculated wilderness kills. Empty use refuses payment;
an unclaimed restart retains the exact bead and progress. Ordinary use consumes
it once, grants the exact non-P7 purse/Chi/rune/80Study/48-hour blessing contract,
and refuses consumed-object replay. A separate cold process retains the full
saved career and blessing. No clock, spirit count or death is supplied. Travel,
proximity, cooldown and timer-free limits remain; natural difficulty, blessing
expiry and native/live acceptance are not claimed.
[Combined evidence](CAREER_STARTING_BOUNDARY.md#fresh-ancestor-worship-and-combat-filled-soul-ball-2026-10-03).
