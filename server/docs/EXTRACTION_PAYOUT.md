# Extraction payout ownership

The previous extraction transaction wrote the new RVP total directly, then
called AddRevoraPoints after commit. That generic mutation compares the saved
purse preimage and could reject its own already-committed reward. The raw UPDATE
also overwrote unrelated saved balance changes without checking that preimage.

Extraction now acquires a nonblocking session lease and character inventory
lock while the event gate is held. This avoids waiting in the opposite order
from session replacement. It requires the canonical ordinary player before
starting and again before commit.

The transaction locks the saved character row and compares its nullable RVP
value with the saved purse preimage. It also checks saved inventory capacity,
then writes the existing claim receipt, items and new balance together. After
commit, ApplyCommittedRevoraPoints updates the live balance and tracked preimage,
and the standard inventory installer publishes the committed items. Reward
amounts, satchel contents, hold timing and failure-forfeiture policy are unchanged.

Focused validation passes 418 counted checks including prerequisites and twelve
new managed-purse assertions. They cover successful claim/autosave/fresh reload,
external RVP changes, item-write rollback, saved-versus-live bag capacity and
replaced sessions. The transaction fixture supplies a completed extraction
member directly; it does not prove the full guarded-cache/boat event route.
The fixture is also included in the ordinary purse-review regression suite.

Logs: ignored `obj/extraction-purse-build.log` and
`obj/extraction-purse-tests.log`. Valuable reward upgrades, full event lifecycle,
real-time combat, native acceptance and deployment remain separate work.

The integrated purse-review regression also passes: 855 logged PASS assertions,
zero FAIL assertions (`obj/extraction-purse-regression.log`). Its summary counter
reports 419 because delegated review assertions maintain a separate counter.
No live deployment, database migration or restart was performed.


## Valuable successful-extraction reward

New ordinary registrations select one catalog P7 soul type. Duplicate registration
returns the existing seat without rerolling. Successful extraction adds that
bound item to the satchel payout, even when the satchel is empty; the RVP formula
still counts only collected relics. The clerk describes the safe-return reward.

Live and saved bag checks reserve space for both satchel and soul. The claim
receipt records soul_type and soul_uid, and the item is inserted with explicit
binding in the same transaction as relics, RVP and claim consumption. Every item
insert must affect one row. Any P7 configured in the satchel pool is also bound
on this new payout path. Previously issued items remain unchanged.

Legacy receipts receive zero defaults for the two new audit columns. Members
without a selected soul retain their original payout. Existing consumed receipts
are never reopened. Temporary satchels and registration remain the event's
process-local state: a restart/disconnect still forfeits unfinished participation
under the existing rules. This batch does not claim resumable event admission.

Focused tests cover ordinary registration, stable selection, exact bound output
and receipt, full-bag retry, failed soul insertion and character reload. The
integrated suite additionally retries consumed valuable claims after reload.
The old managed-purse tests remain unchanged in their reward terms, exercising
the zero-soul contract. Full guarded-cache-to-boat integration with the new prize,
restart lifecycle and native presentation remain separate gates.

Final reward validation: isolated build and 869 logged regression PASS
assertions, zero FAIL assertions. Logs: `obj/extraction-soul-build.log`,
`obj/extraction-soul-tests.log`, `obj/extraction-soul-regression.log` (ignored).
No live migration, deployment, restart or client patch was performed.


## Guarded-cache and boat earning integration

The isolated build and **438 counted checks** pass, including 32 Extraction
route assertions and first-rebirth/character prerequisites. Four ordinary loaded
characters register and enter separated positions on installed 1015 terrain.
The fixture defeats a cache guardian, verifies rival cache contention, waits for
the west boat, rejects early completion and damage/movement interruption, and
retries after a full bag. Parallel completion calls deliver exactly three relics,
the selected bound P7 and the configured 410 RVP increment once.

A normal autosave and production character reload preserve the exact soul UID,
type, binding and final purse. The route also exercises the later Specter, death
forfeiture, transaction rollback and deadline cleanup. Status output uses an
ignored test path through the internal StatusPath seam; production defaults to
its unchanged database/extraction-status.json path.

Logs: `obj/extraction-route-build.log` and `obj/extraction-route-tests.log`.
The fixture supplies the event map on real terrain and controls clock/deaths; it
does not prove Host creation of the dynamic map, live PvP balance, restart
recovery or native NPC/network presentation. Those remain deployment gates.
No live status file, database schema or server process was changed.

## Full process earned-payout recovery (2026-10-01)

`career-extraction-earn` and `career-extraction-reload` now pass against private
full-server executable SHA256
F020C8A2B264B6D420B62A5DFA5421387E11B8D5F6AC44BB8305FEEC1608049B.
Normal startup creates its own dynamic1015-based map with1024x1024 terrain;
this test does not substitute the static field map. A separate non-GM level100
fixture registers via Clerk91002, rejects duplicate seating, enters through the
scheduled Tick, defeats a cache guardian, and claims three temporary relics.
The living-guardian interaction grants no permanent items; repeated opening adds nothing. The west boat
refuses pre-five-minute extraction, nine seconds cannot complete the ten-second
hold, and an interruption notification cancels payout. A fresh hold through Tick
returns the character to Twin City with three relics, one bound P7 and410 RVP.

Another game process and PostgreSQL restart load exactly the saved items, purses,
claim marker, soul UID/type/binding and return map. Repeated completion does not
pay again. `revora_extraction_process_probe` stores the exact fingerprint. The
retained320-achievement career was not changed, and no new achievement is counted.

Fixture eligibility, NPC approaches, event clock, guardian death and damage
notification are controlled. This proves composed event creation and earned
payout recovery, not authenticated/native combat, competitive balance, a lost
COMMIT reply, forced host crash or resumption of an unfinished satchel. The
existing unfinished-participation forfeiture policy is unchanged. Native/live
acceptance remains pending; no gameplay code or production service changed.

The initial bootstrap hit the test UID allowlist after inserting its empty
character row. Recovery removed only that exact named fixture with no Arena row,
items or claim, then completed the corrected bootstrap; no earned state was reset.


## Retained cache combat and stored-item capacity (2026-10-03)

The saved bag-capacity query omitted warehouse and wardrobe filters, the same
predicate defect found in Boss Rush. It now counts position-zero items only when
warehouse=0 and COALESCE(inwardrobe,0)=0. Ownership/purse locks, live capacity,
atomic receipt/items/RVP, countdown interruption and reward terms are unchanged.

`career-retained-extraction-earn` passes with ordinary retained character
4294907001 and its previously earned equipment/belongings. Before entry, the
fixture verifies more than40 position-zero stored items while actual saved bag
count equals the live bag and leaves four reward slots. It does not delete or
move warehouse possessions to make payout possible. Ordinary NPC registration
is replayed without a second member; normal melee calculation kills the
1,543,200-HP camp guardian in15 attacks. The cache yields three satchel relics
once. Early boarding completion and an interrupted hold pay nothing, then a
fresh completed hold returns the player and grants those relics, one bound P7
and exactly410 RVP. The claim receipt points to the actual bound soul item.

The exact nested prior career, items, currency and claim rows survive a separate
game/database restart. Repeated completion adds nothing. New retained checkpoint:
`revora_retained_extraction_probe`, wrapping `revora_retained_rush_probe`'s scope.
Fixture: `tools/CheckCandidateRetainedExtraction.cs`; phases
`career-retained-extraction-{earn,reload}`. Build and both phases pass on private
executable `71F2DA5388C13BBA67FFA936ECBDCB141AE716D21EB2CA78DC151B4AFF380A77`.
Source line endings were normalized back to the original after validation;
the only gameplay change is the capacity predicate.

Registration/boat time, proximity and attack cooldown are controlled. Guardian
retaliation, competitive PvP, natural difficulty, TCP/native presentation and
live deployment remain unverified. This supplies calculated combat and retained
payout evidence, not a new achievement or event-family count. The old predicate
was identified from source and the retained storage counts; this batch did not
run a separate before-fix Extraction payout attempt.

## Fresh guarded cache and escape payout (2026-10-03)

- [x] Continue ordinary FreshRoadmap1 from its paid Beast Trial, with earned
  equipment. Store four owned items through the Ape City warehouse packet.
  Two registration requests create one membership; normal event activation
  places the character in the Bird Island-based event map.
- [x] The guarded cache initially refuses loot. Defeat its1,543,200-HP guardian
  through41 calculated melee attacks; the cache then provides three carried
  relics. A repeated cache request adds nothing.
- [x] An early boat completion and an explicitly interrupted hold give nothing.
  A new completed hold returns the character to town and pays the three relics,
  one bound catalog-P7 and410 RVP. The saved claim references that exact soul UID.
- [x] Cold process recovery preserves all item UIDs, balances, claim rows and
  the nested fresh career. Repeating extraction completion adds nothing.

Fixture `tools/CheckCandidateFreshExtraction.cs` passes
`career-fresh-extraction-{earn,reload}` on the first run. Checkpoint:
`revora_fresh_extraction_probe`. Private executable SHA-256:
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The stored-item capacity regression remains covered: total unequipped owned
items exceed40, while saved actual bag occupancy matches the session.

Registration/boat time, proximity and attack cooldown are controlled. Retaliation,
competitive PvP, natural navigation, difficulty and native presentation remain
unverified. No production source build, live restart or deployment occurred.
