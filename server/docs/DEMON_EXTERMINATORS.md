# Demon Exterminators: Revora v1

M4.1 implementation, 2026-09-15 UTC. Code and isolated automated validation;
deployment and genuine 6711 client acceptance remain pending.

## Valuable daily reward update (2026-09-21)

New contracts additionally award one bound level-1 yellow rune, chosen from
loaded class-neutral yellow rune entries and saved as `rune_reward_id` at
acceptance. This is a Revora economy choice under the owner's rune reward
policy, not an original-game reward claim. There is no daily P7 reward.
Rune equipment eligibility remains governed by the existing rune system.

Migration defaults existing contracts to zero, preserving their five EXP Balls.
The final transaction creates all six new-contract items, consumes the jar,
records every reward UID and completes the achievement together. Five empty
spaces beside the jar are required for the new contract; a full bag retains
the claim. Reconnect and retry retain the promised rune type.

Validation: isolated build and **49 focused Demon hunt checks passed**, including
all four stages through server deaths, transaction rollback, six-item capacity,
concurrent final claims, persisted rune selection, ordinary item reload and old
contract preservation. Log: `obj/chi-refill-validation/demon-rune-tests.log`
(ignored). No unrelated Arena suite was repeated.
Native acceptance and production migration/deployment remain pending.

## Playable route

Cloud Saint (NPC 2000, installed mesh 1640) returns to the Market, map 1036 near
206,213, on a free walkable tile. His original database record remains preserved
on map 5000; runtime placement supplies the restored location.

An ordinary authenticated character of level 120 or higher accepts one daily
quest and receives a bound Cloud Saint's Jar (750000). Complete each stage and
bring the jar back before beginning the next:

| Stage | Souls | Target | Map / family IDs |
| --- | --- | --- | --- |
| 1 | 600 | Birdmen | Bird Island 1015 / 18 |
| 2 | 1,200 | Tomb Bats | Mystic Castle 1001 / 20 |
| 3 | 1,500 | Bull Monsters | Mystic Castle 1001 / 57 |
| 4 | 1,800 | Red Devils | Mystic Castle 1001 / 58 or 83 |

The [official Demon Exterminators guide](https://co.99.com/guide/quest/demonexterminators.shtml)
establishes the minimum level, daily chance, four counts, team sharing, exclusion
of guard kills and a reward worth five EXP Balls. Revora v1 chooses the fixed
target sequence above from installed, populated hunting families. It delivers
**five bound event EXP Ball items (722136)** at final turn-in, rather than directly
applying experience. These two choices are explicit Revora implementation rules.
Existing EXP Ball consumption rules still apply: below level 137 and at most ten
uses per day. This quest does not change leveling or item-use rules.

Kills count only for the matching stage and map, with the tracked jar held in
the persisted inventory. A nearby eligible teammate in the same team and map can
share a real player's kill. Fake adventurers, owned guards/companions, distant
teammates and other monster families cannot supply souls. Counters stop at each
stage's requirement. Team sharing uses the existing screen-distance limit.

The **fourth turn-in** atomically consumes the jar, creates five bound EXP Balls
and the saved bound yellow rune, and records achievement **11001**. Five empty
spaces beside the jar are required for new contracts; pre-update contracts without
a rune still require four. A full bag leaves the completed hunt claimable. Repeated requests
cannot create another daily reward or extend eligibility. Buying EXP Balls or
changing the old `Status4` counter never proves quest completion.

The day changes at midnight **America/Los_Angeles**, including daylight saving.
An unfinished quest stops accepting progress after that boundary. Accepting the
next day's quest replaces an older tracked jar in the bag without deleting its
historical progress row. Abandonment requires a confirmation option, returns the
jar and uses that day's chance. A stored jar must be brought back to the bag.
The original 98-CP shortcut and level-120 invitation-letter reward are not enabled
in this implementation.

## Ownership, persistence and client interface

`revora_demon_runs` stores one row per character/day: stage, capped count, tracked
jar UID, terminal state, reward item UIDs and completion time. It is additive;
legacy character counters are preserved but no longer authorize completions.
There is no per-kill database ledger or unbounded pending-reward queue.

The relevant monster's death path rejects repeated deaths for the same life.
Death coordinates and family are captured before processing. Each accepted soul
is committed to the quest row. Row locks serialize kill progress and NPC turn-in;
admission also locks the character row. Inventory mutation uses the character's
item lock. Counter-only team credit never takes another player's item lock,
avoiding deadlock when two teammates cause kills while using items.

The final exchange and achievement receipt share one transaction. Inventory and
achievement updates follow commit. A failed mutation fences further actions
until reconnect; an uncommitted kill is not displayed as saved progress. Normal
inventory loading recovers committed jars/rewards, and login or jar inspection
reconstructs active membership from durable state. A restart retains an unfinished
hunt within its day. A completed achievement remains permanent across later days.

NPC dialogue shows the current target and count; chat reports every hundred souls
and stage completion. The existing jar count request returns authoritative data
with the current character's identity and is rate limited. It never imports the
requested counter. Old solo/team jar increments were removed.

Native mission 6012 belongs to **Crisis of Evil**, a different weekly quest.
It is not reused. A native quest-tracker entry and any required client content
remain future work; the NPC/jar route does not advertise that tracker as complete.
See [protocol evidence](PROTOCOL_RESEARCH.md).

Private client data was inspected read-only. Both item identities exist in the
installed client and server catalogs. Client SHA-256 evidence:

- `ini/itemtype.dat`: `d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`
- `ini/npc.ini`: `b06db1e2484992a1b58eabd7a3d884da07ac728a4a183d0fd18f2dccd5b20497`
- `ini/Questinfo.ini`: `f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`

No proprietary catalog, client binary or decoded file is added to Git.

## Validation and rollout

The isolated executable passes **44 Demon hunt checks** and **280 regressions**
(78 Elite PK, 48 CTF, 63 progression, 91 wardrobe): **324 checks total**.
The full 5,100-kill route exercises ordinary-player NPC dispatch and actual server
monster deaths; fixture loot is suppressed. Checks include simultaneous team
kills, duplicate deaths, guard/fake exclusion, wrong map/range/family, saved jar
ownership, failed progress and final writes, full inventory, repeated claims,
ordinary item-loader recovery, abandonment and summer/winter daily boundaries.

Build to `obj/chi-refill-validation/bin/` using the repository's existing isolated
legacy build settings. Run `tests/PotencyHunt/Verify-PotencyHunt.ps1` separately
with `-DemonOnly`, `-ElitePkOnly`, `-CtfOnly`, `-ProgressionOnly` and `-TitlesOnly`.
Run sequentially: all use the disposable `revora_potency_validation` schema, which
is removed after each run. No game listeners or live server entry point are run.

Validated staged executable SHA-256:
`78217896c2ec9314eff95d2e5b58ad253ebdf2aa13c30eca8e7141ac742d0d12`.

No live migration, server replacement/restart, client patch or website release
was performed. Before activation, preserve the current binary and database,
review M1's legacy-achievement migration, then validate the NPC, jar display,
team sharing, four turn-ins, inventory, achievement and reconnect with genuine
6711 clients. Rollback restores the prior executable while preserving quest and
award evidence; do not overwrite later player progress to revert quest code.

## Retained career: four hunts and cold recovery (2026-10-01)

The retained ordinary Archer completes all 5,100 required kills through normal
`Attack.HandleAttack` ranged combat against loaded Birdmen, Tomb Bats, Bull
Monsters and Red Devils. Installed portals connect Twin City, Bird Island,
Desert City, Mystic Castle and Market; Cloud Saint's ordinary NPC dispatcher
accepts the jar and advances each stage. No quest counters or rewards are seeded.
Each stage runs in a separate game/database process, and the next phase checks
exact saved career and quest state before continuing. The final filled jar also
survives a process restart before claiming.

At 36 occupied bag slots including the jar, final turn-in refuses without any
saved-state change. One owned unbound epic spare weapon is transferred to the
existing ordinary peer through decoded trade requests, freeing the fifth required
space. Final turn-in consumes the jar and creates five bound EXP Balls plus the
saved bound yellow rune, with achievement 11001 in the same transaction. Repeated
turn-in and same-day acceptance change nothing. A further cold restart retains
all six reward UIDs, the receipt, prior career and peer ownership; jar count is 0.
No daily P7 reward is issued. The main bag ends at 40; nothing is discarded.

All six phases pass: `career-demon-0` through `career-demon-3`,
`career-demon-claim`, and `career-demon-reload`. The initial harness attempt was
refused by its phase allowlist before startup or player mutation; that omission
was corrected before the six passing phases.
Private executable SHA256:
`17976314CE4445B573179FF4AC744F2581196BA7E242D2A6BE2A09C31AF713A6`.
The new checkpoint is `revora_career_demon_probe` step 5 through
`CareerDemonSnapshot`; the prior Power Arena checkpoint is historical.

Controls: the hunt clock is fixed to 2026-10-05 Pacific; approach coordinates,
attack cooldown and production monster-respawn deadlines are controlled. The
retained high-level equipment defeats each target in one normal ranged shot.
This proves server earning and persistence, not natural travel timing, encounter
balance, wire authentication, power-loss durability or native client acceptance.
No production build, deployment, restart or client release was performed.

## Fresh four-hunt earning and rune payout (2026-10-03)

- [x] Continue ordinary FreshRoadmap1 from its paid Flame Lit checkpoint.
  Preserve existing possessions, enter the Market through its ordinary portal
  and accept Cloud Saint's jar. Early turn-in is refused without progress loss.
- [x] Earn all four quotas through calculated melee:600 Birdmen,1,200 Tomb Bats,
  1,500 Bull Monsters and1,800 Red Devils. Attacks per stage are602/1,200/2,825/
  8,265:5,100 kills and12,892 attacks total. No damage value, death callback,
  level, kill count or reward item is supplied.
- [x] Each stage's saved jar/progress and nested career survive a separate
  game/database process. The last full jar is saved before the final claim.
- [x] Cold final turn-in consumes the jar and grants exactly five bound EXP
  Balls722136 plus the accepted contract's bound rune. All six saved reward IDs
  remain owned, the contract completes and11001 is recorded. No P7 is awarded.
- [x] Repeated completion/admission pays nothing. Another full restart preserves
  exact rewards, profile, quest and previous career; the jar count stays zero.

Fixture `tools/CheckCandidateFreshDemon.cs` passes all six phases on the first
run: `career-fresh-demon-{0,1,2,3,claim,reload}`. Checkpoint:
`revora_fresh_demon_probe`, step5. Private game SHA-256:
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.

Quest day2041-03-25, approach, attack cooldown and map respawn deadlines are
controlled. Real portal/NPC/item/combat handlers are exercised, but natural
navigation, retaliation, timing/balance and native acceptance remain open.
The rune and EXP Balls remain owned/unopened in this batch. No production
rebuild, live restart, deployment or client patch occurred. The post-Flame
receipt audit predates this additional achievement.
