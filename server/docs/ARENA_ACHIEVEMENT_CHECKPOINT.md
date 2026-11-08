# Arena achievement implementation checkpoint

Source review, 2026-09-25. The initial review below predates the first-nine implementation appended below. This checkpoint
separates client-confirmed identities from the persistence work needed to earn them.

The installed `ini/Achievement.dat` SHA-256 is
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
The existing decoder passes its hash gate. Numeric identities and paraphrased
requirements below are local-client evidence, not a claim about unpublished rules.

| IDs | Client requirement |
| --- | --- |
| 11101-11107 | Arena victory against Trojan, Warrior, Fire Taoist, Water Taoist, Archer, Ninja and Monk respectively |
| 11108, 11109, 11309 | Arena victory against Pirate, Dragon Warrior and Windwalker respectively |
| 11111 | Participate in twenty tournaments within a day |
| 11112 | Win the first nine tournaments within a day |

## Findings that constrain implementation

- `ArenaDailyRewards.Record` grants its existing nine-win reward from total daily
  wins. A loss followed by nine wins can qualify for that reward but cannot prove
  achievement 11112. Keep the reward intact; implement a separate predicate over
  the first nine ordered results. Do not award from the `wins9` payment receipt.
- `ArenaResultStatistics.Record` writes unique run/player result receipts and
  `result_order`, with `ordering_verified=true` for new results. Legacy rows default
  to unverified. Fail closed for days with unverified ordering rather than treating
  migration sequence allocation as historical match order.
- `ArenaStartupRecovery` independently writes result statistics after loading
  saved outcome intents. A producer added only to the live result method would
  miss recovered victories. Both paths need the same transaction-bound evaluator.
- `ArenaResultIntents` snapshots statistics, VIP, reward policy and balances, but
  not class. Reading the loser's current class during delayed recovery would be
  incorrect. Capture the class with the outcome before awarding class victories;
  old snapshots with unknown class must remain ineligible for those achievements.
- `ArenaParticipation` retains only a first-participation receipt per player.
  It cannot count twenty accepted entries. Result receipts count completed fights,
  which is a different boundary. Define the Revora participation policy explicitly
  and preserve each accepted entry before implementing 11111; never use queue
  requests or a supplied client counter as participation evidence.
- The current source is wired through `Server.Run` calling
  `ArenaStartupPreparation.Prepare`, which initializes the coordinator and recovery
  before caches/listeners. Older documents and class comments calling these
  components inactive describe historical stages, not today's source wiring.
  This says nothing about the version installed in the live server.

## Next coherent implementation batches

1. Implement 11112 with one shared evaluator invoked inside live and recovery result
   transactions. Use saved Pacific outcome day and verified ordered result receipts;
   award only when the first nine results are all victories. Require no legacy or
   pending earlier outcome to be silently skipped. Refresh online achievements
   after commit; login must load the same durable evidence after restart.
2. Capture outcome classes durably, map each supported class family using reviewed
   server class identities, and record winner-only class achievements in both
   result paths. A changed class, repeated callback or recovery cannot reinterpret
   the original opponent. Explicitly review timeout/forfeit/disconnect eligibility
   against the established Arena result rules.
3. Add durable accepted-entry/day evidence for 11111 after documenting its Revora
   boundary. Keep entry evidence distinct from completed-match reward counts.

Acceptance for the first batch: eight wins fail; first nine wins pass; an early
loss followed by nine wins fails; later losses do not revoke completion; another
Pacific day starts a fresh sequence; legacy unverified results fail closed; result
rollback grants nothing; concurrent replay grants once; captured outcome recovery
and production achievement reload agree with the live path. Include midnight and
pending-outcome ordering checks before describing this as an ordinary earning route.

No producer count changes, reward changes, deployment or native acceptance are
claimed by this review. Existing native/live gates remain open.

## First-nine implementation (2026-09-25)

Achievement 11112 now has a shared transaction-bound evaluator in
ArenaResultStatistics.RecordFirstNine, called by live result statistics and cold
startup recovery. It reads the first nine finished results of the saved Pacific
day, ordered by observed completion time and verified receipt order for ties.
Every one must be a win. Unverified ordering anywhere in that day refuses new
completion. Any active, non-abandoned fight also prevents completion until settled.
A later loss cannot revoke an already-earned receipt; repeated calls cannot duplicate it.

The completion joins the result/statistics transaction, so failure to save its
receipt rolls back settlement. Normal projection refreshes achievements after
commit; cold recovery leaves the same receipt for the production achievement loader.
Existing nine-total-win rewards are unchanged. Classes and twenty-entry achievements
remain unimplemented. Historical achievement backfill is not introduced.

Validation covers ordinary imported match results, the eight/nine threshold,
an early loss followed by nine wins, unverified historical order, concurrent
settlement, suppression of the recovery achievement insert, restart recovery and
production achievement loading. Match outcomes and qualifying characters are
controlled fixtures, not native-client combat acceptance. A dedicated overlapping
pending-outcome ordering fixture and native/live acceptance remain open.

The isolated Debug/x86 build, 1,191 Arena daily/prerequisite checks and five
producer-inventory audit tests pass for this batch. Unrelated owner changes were
present in the isolated assembly. Source coverage is 262/337, with 75 missing
producers; this is not a count of playable or native-accepted achievements.
No deployment, restart or client patch was performed.

## Overlapping and incomplete outcome validation

The evaluator now checks every finished match involving the character on that day
for a matching player result receipt. Missing receipts, unverified order and a
`won` flag contradicting the match's saved winner refuse completion. Counting is
also limited to matches that actually include the character.

The fault fixture imports and settles nine ordinary winning matches while an
injected earlier active match remains unresolved. No completion is awarded. That
earlier match is then marked as a loss with, successively, a missing receipt, a
contradictory winning receipt and the correct losing receipt. All three states
refuse first-nine completion. The overlap and damaged receipts are intentionally
injected database states, not a claim that normal admission permits overlapping
matches. This closes the dedicated fault-ordering fixture gap; genuine-client and
live deployment acceptance remain open.

Ordering repair validation: isolated build, 1,204 Arena daily/prerequisite checks
and five inventory audits pass. No production deployment or native-client test.

## Captured opponent class victories

New outcome snapshots store the opponent's live class while both original
participants and the owned result are locked. Repeated capture preserves that
value. The nullable `opponent_class` migration leaves old outcomes unknown rather
than backfilling from a character's current class. The snapshot is read by both
normal settlement and cold recovery; only the winner receives a completion.

Mappings use the installed client identities above and existing server profession
families: 10-15, 20-25, 140-145, 130-135, 40-45, 50-55, 60-65, 70-75, 80-85 and
160-165. DataHolderTable's Water/Fire predicates, Entity class ranges and current
promotion definitions supply the server-side evidence. Unknown families or missing
snapshots award nothing. There is no weapon-based class inference.

Revora uses the existing authoritative solo Arena winner for these achievements,
including the established timeout, forfeit and disconnect outcomes. This is an
explicit local result policy, not a claim that the client text proves every
original-server edge case. Team Arena is not silently substituted for solo Arena.
No currency or item reward changes are introduced.

Validation exercises all ten classes through normal settlement and cold recovery,
changing the opponent's class after capture in each case. Winner-only receipts,
result replay and fresh production achievement loading are checked. A suppressed
class receipt rolls back the live result/statistics bundle; the saved intent then
recovers exactly one completion. Unknown and migrated-null classes cannot mint an
inferred completion. Fixtures supply qualified players and controlled match endings;
genuine-client combat and live deployment remain pending.

Class-victory batch validation: isolated build, 1,268 Arena daily/prerequisite
checks and five inventory audits pass. Source producer coverage is 272/337;
65 producers remain absent, and at least 48 more identities are needed before
320 is even a source-level upper bound. This is not per-character or native
acceptance. Unrelated owner changes were present in the isolated build.

## Twenty accepted solo Arena entries

Achievement 11111 now uses a distinct `(character, match)` entry receipt written
only by registered solo Arena admission after both accepted fighters are imported
onto the owned match map. It shares the ledger/first-participation transaction.
The day comes from the ledger's saved admission timestamp, using Revora's existing
Pacific day policy. Twenty such entries on one day complete the achievement for
both fighters. Queue clicks, watchers, unregistered legacy calls and repeated
import callbacks do not advance this count. The existing twenty-completed-match
Honor reward is unchanged; it has a different boundary.

New match ledgers mark entry evidence version 1 in the same transaction. Interrupted
entry recovery requires both daily entry receipts for those ledgers; migrated
version-0 matches keep their original recovery contract. No historical daily
participation is inferred from first-participation or completed-match counters.
Daily entry insert failure rolls back the ledger and both participants' evidence.
Postcommit projection continues through the established fenced recovery path.

This is a documented Revora interpretation of tournament participation. Eligible
ordinary players use the same registered admission path; there is no GM gate.
Native UI and live deployment remain pending.

Entry batch validation: isolated build and 1,342 Arena daily/prerequisite checks
pass, plus five inventory audits. The ordinary registered-entry fixture imports
and settles 21 distinct matches, checks both threshold recipients before the
20th result, replays every entry, enters on another Pacific day, and reloads the
achievement from durable evidence. Injected second-player entry failure rolls
back the ledger and participation bundle, then exercises fenced cancellation and
cleanup. Qualified players and match endings are controlled fixtures. A dedicated
11111 receipt-suppression case and malformed daily-entry recovery case remain
additional acceptance work; native/live gates remain open. No deployment occurred.

## Daily-entry recovery completion

Interrupted entry recovery now validates the receipt day against the saved admission
time using the same Pacific conversion as creation. Version 0 retains the old
migration contract; version 1 requires the two matching daily receipts; other
versions are rejected. Missing receipts, wrong opponents, wrong days and unknown
versions retain recovery ownership instead of accepting or cancelling a committed
fight. Restoring the original fixture evidence permits the normal recovery path.

The achievement-failure fixture earns nineteen entries through ordinary registered
imports and completed results. A database trigger then suppresses achievement
11111 at entry twenty. Both players retain nineteen saved entries and no 11111;
the new match ledger rolls back, both sessions are fenced, the reserved admission
is cancelled through owned recovery, and cleanup releases the map after session
release. No production data is altered by these disposable-schema faults.

This closes the two dedicated source-side acceptance cases listed in the previous
batch. It does not replace native-client entry/reconnect acceptance or authorize
live deployment.

Final entry-recovery validation: isolated build, 1,407 Arena daily/prerequisite
checks and five inventory audits pass. Unrelated owner work remains outside this
commit; it was present in the isolated build. No deployment or client patch.

## Retained twenty-match route (2026-10-01)

The ordinary retained Windwalker character completed twenty queued solo matches
through sealed signup/accept packets, the production matcher and actual melee
combat. Nine supplied ordinary opponents cover Warrior, Fire Taoist, Water
Taoist, Archer, Ninja, Monk, Pirate, Dragon Warrior and Windwalker. Together with
the previously earned Trojan victory, all ten class-victory receipts persist.
Daily nine-win (11112) and twenty-entry (11111) receipts are saved for 20280515.

Both daily milestones deliver their EXP Ball and bound rune to owned inventory.
A separate process restores the complete retained career and Arena result,
statistics and reward snapshots. Repeated reward payment calls preserve the
exact saved snapshot and create no duplicate items. The Grail, siege and earlier
career state remains part of that comparison.

Validation: `career-retained-arena-earn` and `career-retained-arena-reload` passed
on candidate game SHA256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
The first attempt stopped during opponent setup before any match: the newly
seeded fixture had not loaded its durable save state. New opponents now load
through `LoadEntityState` before their class setup/save. Existing fixture rows
were retained; no main-character reward or match history was reset.

Limits: opponent class/level prerequisites are supplied, calendar and combat
cooldowns/positions are controlled, and opponents do not fight back. This proves
the ordinary earning/result/reward route, not class balance, opponent progression,
native UI, live deployment or real-time twenty-match duration. No production code,
client patch or live server was changed for this batch.

## Fresh twenty-match day and earned class opponents (2026-10-03)

The fresh career continues from the completed Couple PK checkpoint. Its main
level 133 Warrior fights the earned level 130 Warrior, level 70 Archer and level 70
Monk in rotation. Their classes, levels, equipment and purses are loaded unchanged;
no new opponents or eligibility assignments are introduced.

Normal sealed signup/acceptance packets, matchmaking and calculated melee
produce 20 wins with 20 hits during the controlled October 20, 2041 day. Each
completed match is saved before the next begins. The existing controlled town
return is completed before saving; ordinary revival is used if needed. Receipts
11102/11105/11107 prove the three new class victories, 11112 records the first
nine wins, and 11111 records twenty entries. The six remaining opponent-class
paths are not claimed by this batch; the earlier Trojan win remains preserved.

After a separate game/database restart, the production ranking settlement
closes the day and pays exactly 1,000,000 Honor to the main Warrior, 999,000 to
the other Warrior, 998,000 to the Monk and 997,000 to the Archer. These are the
private four-player standings, not a claim about public ranking competitiveness.
Repeat payment changes nothing. A further restart validates loaded solo/team
Honor mirrors, exact Arena rows, twenty results/statistics, daily entries, rank
receipts and the complete nested prior career.

The first run stopped on a combined admission/match assertion after six saved
victories. Diagnostic reload found zero differences from the saved snapshot:
`0069529016f183f4c5001e82e10ba032180c8294966fd219454880569d928479`.
The fixture now accepts either both queued players or an already-matched pair,
accounting for automatic matchmaking. It resumes at match seven; no result,
profile, reward or checkpoint is reset. The resumed fight, payment and final
reload all pass. This required no production-code change.

Evidence: CheckCandidateFreshSolo.cs and career-fresh-solo-fight/pay/reload,
private candidate SHA256
D48A6C00F6BF84889581ADFEABEB924C4C7448C5DC3C6D17F8674129C43665B6.
Calendar, proximity, combat protection, cooldown and return timing are controlled;
opponents do not retaliate. This is server earning and persistence evidence,
not PvP balance, natural match duration, native acceptance or live deployment.
