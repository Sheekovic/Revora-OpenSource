# Duel Pit reward claims

## Fresh five-player calculated combat and cold prizes (2026-10-03)

The fresh main UID1096322 joins four existing ordinary contributors:
1096324/1096325/1096326 at level50 and1096323 at level70. Their original creation,
training and possessions remain in the nested career. All load alive; no new
character, gear, currency or combat statistic is supplied. The main deposits
two existing items through the Ape City warehouse, leaving38 bag slots occupied.
All five approach the real DuelPit clerk and preserve preparation over restart.

Ordinary registration admits five players to the composed dynamic base700 arena.
Four rounds use `Attack.HandleAttack` melee calculation, taking16/15/9/2 attacks
respectively:42 total. No damage amount, death, score or standing is injected.
Each of four recipients earns one win, and the main wins the final round. The
no-opponent wait settles four ranked results. All five durable admissions end
and return to Twin City, restoring their pre-event PK modes on normal return.

The next game/database process claims each placement through the ordinary NPC:

| Place | CPs | RVPs | Silver | Item |
| --- | ---: | ---: | ---: | --- |
| 1 | 12,000 | 32 | 1,000,000 | One bound P7 |
| 2 | 7,000 | 18 | 500,000 | One bound P7 |
| 3 | 4,000 | 10 | 250,000 | One bound P7 |
| 4 | 2,000 | 6 | 100,000 | One bound P7 |

The zero-win player receives nothing. Each purse delta matches its configured
placement; each winner gets exactly one new bound P7 while retaining all prior
bag UIDs. Repeated claims change neither purse nor inventory. A separate cold
reload preserves the full roster/career/result/admission/variable snapshot,
verifies every loaded bag UID against persisted physical inventory, and leaves
all five alive in town with no further payment.

`career-fresh-duel-prepare`, `career-fresh-duel-earn`,
`career-fresh-duel-claim` and `career-fresh-duel-reload` pass on first execution.
Only final cold replay was repeated after adding the explicit loaded-bag check;
no earning or payout was repeated. The authoritative checkpoint is
`revora_fresh_duel_probe`, stage3, with `FreshDuelSnapshot(p, players)` in roster
order1096324,1096325,1096326,1096323,1096322. Historical Race/Quiz checkpoints are
not current baselines after this advancement.

Source: `tools/CheckCandidateFreshDuel.cs`. Redacted logs:
`C:\Revora\DisposablePostgres\moon-box-candidate\arena-process-career-fresh-duel-*.log`.
Unchanged private game SHA-256:
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Event registration/deadlines, attack stamps, proximity and attacker order are
controlled. This replaces the earlier supplied lethal-damage boundary for this
fresh path, but does not prove natural player tactics, competitive balance,
24-player/16-round load or genuine6711 presentation. No production code, live
restart, deployment or client patch changed.

## Composed-process standings and wardrobe-safe claims (2026-10-01)

Production startup now supplies the actual dynamic map-700 arena for a retained
five-player journey. Ordinary NPC registration, four contested rounds, the
no-opponent timeout, four saved placements and all five town returns execute in
the composed private server. Each qualifying recipient earned one win; the
zero-win participant receives no prize. Original PK modes are restored on normal
return. Event clock/deadlines, proximity and lethal `ReceiveAttack` damage are
controlled; neither ranks nor wins are injected. This is not native combat or a
natural damage/balance test, nor another 24-player/full-16-round acceptance run.

The first claim process exposed a player-facing bug: the retained winner had
39 bag items but the old database capacity query counted 338, including 299
wardrobe items. The claim safely retained its entitlement but could never pay
while the wardrobe remained populated. The query now excludes `inwardrobe`
items, matching the physical inventory count. It still checks both the in-memory
and saved 40-slot limit inside the existing transaction.

Three other placements had already received their exact configured CP, silver,
RVP and one bound P7 before that assertion stopped the fixture. A guarded resume
verifies their purse deltas against the saved pre-claim profiles, the exact added
bound-P7 item per winner, zero additions for the zero-win player, and consumed
entitlements. It verifies the first-place winner's unchanged purse, pending
placement and 39-item bag before retrying the ordinary clerk. No earlier reward
is repaid, rolled back or fabricated. The formerly blocked first-place claim
then succeeds exactly once, filling the bag to 40.

`career-duel-earn`, `career-duel-resume` and `career-duel-reload` pass. The last
phase cold-loads the full snapshot, replays all five claims and compares exact
saved state again. Purchase-free capacity preparation transferred one existing
item to the retained peer. An initial pre-admission fixture retry corrected the
peer's missing variable-vault load and restored only controlled approach
coordinates; no prize was involved in that retry.

The isolated source build succeeds with existing warnings. The stopped private
candidate was refreshed with rollback artifacts preserved; executable SHA-256:
`A832A0A2E2AAD22B60CB757DC16A87394471B0DF481DF0432E2AC8AEED56E36B`.
No live restart, deployment or client release. This closes composed dynamic-map
startup and earned-result/claim cold recovery, not in-flight Host interruption,
network stress or native presentation gates.

## Implemented 2026-09-26

Each valid saved first-through-fourth placement now pays its configured CP,
silver and RVP purse plus one bound random catalog P7 soul. This applies to
pending saved placements too; consumed placements remain consumed. Selection
occurs at claim time. This is an event reward, not a daily quest reward.

The ordinary character claim holds the session lease, inventory lock and
variable-vault lock. One database transaction compares saved purse preimages,
checks the saved placement and inventory capacity, consumes the placement and
inserts the soul with all three currency updates. Unrelated variable changes
are merged. Only committed state is installed and sent to the character.
Full inventory or purse overflow retains the prize; transaction failures fence
the session for reload and roll back the entitlement and payout together.
Invalid placement numbers and replaced sessions cannot claim.

## Validation

The staged source build and focused disposable-database suite passed:
434 counted checks, comprising 406 first-rebirth fixture prerequisites and
28 Duel Pit assertions. Ordinary managed characters cover all four placements,
exact bound soul UID/type and currency reload, post-claim autosave, concurrent
replay after reconnect, failed item insertion and recovery, externally changed
RVP, saved-only full inventory, invalid placement and stale-session rejection,
and preservation of unrelated externally saved quest variables.

Artifacts: ignored `obj/duel-prizes-build.log`, `obj/duel-prizes-tests.log`.
Tracked checks: `VerifyDuelPrizes` in
`tests/PotencyHunt/ConquerPointBagTests.cs`, also called by the purse review suite.
The broader purse suite was not rerun for this isolated claim change.

## Durable results and earned route ? 2026-09-26

`Create` initializes `revora_duel_results`. Finish freezes deterministic standings
(king, wins, elimination round, then UID), writes the entire top-four eligible
roster in one transaction, then notifies connected recipients and clears the
arena. Offline winners keep their actual placement. Failed publication retains
the frozen roster for the next pulse; repeated publication verifies the same
rows and does not create another prize.

At the clerk, a character with no existing claim imports their oldest pending
result. The result's imported flag and vault entitlement commit together under
the session, item and vault locks. Existing legacy prizes remain first in line;
later tournaments queue separately. Imported results are retained as receipts.
The ordinary claim transaction described above consumes each entitlement.
Configured purse values and random soul selection remain claim-time terms.
Registration now requires a canonical, loaded ordinary session.

Final staged build and focused suite: **453 checks**, consisting of 406 fixture
prerequisites, 28 claim assertions and 19 result/route assertions. Result checks
cover offline ranking, failed all-or-nothing publication, frozen retry, duplicate
finish, two queued tournaments, preservation of a legacy pending prize, atomic
import failure, reconnect between import and claim, and draining all prizes.

A two-player route uses real map-700 terrain in a fixture-supplied arena,
production registration and StartFight, a controlled death callback, the
no-opponent timeout, result publication, town return and reward claim after
reconnect. It does not inject a placement for that winner. The test supplies HP
because the offline character fixture does not perform native world-entry stat
initialization. It is not native combat, full damage-formula validation or a
production dynamic-map startup test.

Artifacts: `obj/duel-results-build.log`, `obj/duel-results-tests.log` (ignored).
Tracked `VerifyDuelResults` runs beside `VerifyDuelPrizes` in the purse suite.
Only the focused suite was run for this batch.

## Admission and interrupted-match recovery

Before a match starts, `revora_duel_admissions` saves every admitted UID, arena
and original PK mode in one transaction. Canonical sessions are checked under
UID-ordered session leases and inventory locks. A previous unreturned admission
blocks a new one. Failed admission writes leave the match unstarted.

The recovery policy follows the existing Relic Rush policy: startup cancels
unfinished matches; it does not resume combat or infer a winner from position.
Published result rows and prize claims remain intact. Death, disconnect and
normal finish end the corresponding admissions. Login and the event tick retry
pending returns. Each return restores the original PK mode, sends its update,
teleports to the exact Twin City return tile and saves the character before
acknowledging the admission. A failed acknowledgement retains the pending return
and fences the session; reconnect retries safely. Queued fighters are included.

Validation: staged build and **465 focused checks** (406 prerequisites + 59 Duel
Pit assertions). Added checks cover all-or-nothing admission, normal winner and
loser modes, interrupted admission on a replacement session, no invented winner,
failed return acknowledgement/reconnect and preservation of published prizes.
A 24-player fixture runs all 16 rounds through controlled death callbacks,
rejects a 25th registration, publishes four placements and verifies all 24
players return with restored modes and durable acknowledgements.

Artifacts: `obj/duel-recovery-build.log`, `obj/duel-recovery-tests.log` (ignored).
The production login hook calls recovery before continuing later login services.
These are service/reload tests, not an actual Host process kill/restart or native
combat test. Source remains staged: no live restart or database migration.

## Unfinished-match process interruption (2026-10-01)

Four retained ordinary players registered through the installed NPC. Production
StartFight saved all four admissions and started round zero: two active fighters
entered the dynamically created arena, while two queued players remained in town.
The process exited without normal shutdown before any result was published.

On a fresh game/database process, startup cancelled the unfinished admissions.
Repeated production login recovery returned all four to the exact town tile,
restored each reserved PK mode and durably acknowledged the return. Claim attempts
created no prize. Exact saved items, all three purses, variable vaults and prior
Duel results matched the pre-admission snapshot. Another abrupt exit and cold
reload verified the same assets, acknowledgements and absence of a new result.
General PK mode persistence outside an outstanding return is not claimed.

The first fixture incorrectly expected all four players in the arena and exited
at that assertion after admissions committed. Recovery used those actual records;
it did not replay admission or manufacture a winner. The assertion now checks
two fighters and two queued players. The recovery phase itself passed; its wrapper
initially rejected the new output marker. That marker mapping was corrected and
the subsequent cold reload passed. The initial admission run is not reported as
an entirely green fixture run.

Evidence: `DuelInterrupted` in `tools/CheckCandidateCareerRites.cs`, phases
`duel-interrupt-enter`, `duel-interrupt-recover`, `duel-interrupt-reload`; private
process logs of those names. Executable SHA-256:
`6DE2AAA400756A51F82CFD7B58717FE7504461AF4302562012D7C2A991A6B2FC`.
Calendar/start boundaries are controlled; no combat damage or network stress is
exercised by this interruption route. No deployment or live restart occurred.

## Remaining gates

Simultaneous network combat/disconnect stress,
native NPC/HP/reward presentation and deployment remain pending. The composed
startup and earned-result/claim restarts above supersede those earlier open
process gates. The controlled full-capacity route does not verify native damage formulas
or timing under network load. Interrupted unfinished matches are cancelled; only
committed results retain prizes, rather than reconstructing uncommitted scores.
