# Boss Rush victory reward

New admissions freeze one catalog P7 soul type in the existing durable progress
row. Repeated admission/checkpoint writes preserve that selection. A completed
victory claims one bound soul alongside the configured currency and Perfection
Stones, in the existing claim transaction. Recovery reads the same progress row;
it does not reroll the reward or reopen a consumed claim.

Migration defaults existing progress rows to zero. Existing issued and legacy
variable-only claims keep their former terms. Boss-three consolation gets no
soul. The reward is for this scheduled event, not a daily quest.

The claim validates the saved type against the loaded P7 catalog and checks both
live and saved inventory capacity before consuming the result. Item insertion,
purses, result consumption and claim-vault transition commit together. A failed
item write rolls them back; full inventory keeps the claim pending. The normal
postcommit item installation makes the bound item available immediately.

Validation: isolated build and **407 purse-review checks** pass, including
30 new Boss Rush contract checks. Logs are ignored `obj/boss-rush-soul-build.log`
and `obj/boss-rush-soul-tests.log`. Added fixtures drive production
admission checkpoints, result issuance and claims, checking frozen selection,
full inventory, item-write rollback, consolation exclusion, fresh character
reload and replay. They deliberately control the combat outcome; they do not
prove five-boss combat or native client presentation. Existing legacy-claim and
purse regressions run in the same suite. No live deployment or client patch.


## Integrated registration-to-prize route

An ordinary level-140, twice-reborn fixture now enters through the same
registration service called by the NPC. Production admission allocates the
dynamic arena on installed map-700 terrain and saves the reward contract.
The fixture verifies exactly one live boss for each of the five stages, uses
the production ReceiveAttack/death path with controlled lethal damage, and
lets the event tick advance and settle victory. The NPC claim then installs
the frozen bound soul, followed by a fresh character reload.

The focused run passes 421 checks including prerequisites and 15 route checks.
Log: `obj/boss-rush-route-focused.log` (ignored). This exercises registered
entry, catalog spawns, damage/death dispatch, stage advancement, return and
reward persistence. It does not test player damage calculation, difficulty,
real-time AI, network NPC framing or genuine client presentation.

Follow-up discovered during route review: terminal metadata removal does not
dispose the allocated map, and failed returns can lose the active registry
entry before the character exits. Arena cleanup/return retry needs an owned
lifecycle repair before claiming complete event lifecycle acceptance.

The full purse regression run also passes with 840 logged PASS assertions and
zero FAIL assertions (`obj/boss-rush-route-tests.log`). Its legacy summary
counter prints 407 because the delegated review assertions use a separate
failure counter. The focused route uses the normal counted assertion helper.
No production code changed in this validation batch.


## Owned arena closure and durable return

New admissions record the allocated arena and an unacknowledged return in the
existing progress row before teleporting. Old rows default to returned, retaining
their prior contract. Shared admission first recovers older work and refuses a
new run while a saved return is pending.

Terminal runs queue only arenas created by this event. The bounded world-tick
cleanup resolves the current canonical session by character ID, retries terminal
returns under its session lease and nonblocking character/virtue/PK locks, and
saves the town destination before acknowledging the return. A reconnect can
recover the same obligation from the progress row independently of the old
session. Cold active/admitting rows use the existing abandoned-run recovery
before becoming eligible for return. Reward consumption remains separate.

Cleanup waits while any connected character remains in the arena. It removes
entities, floor items, the PK-free registration and the map only when the map
registry still contains that exact allocated instance. Synthetic/borrowed arenas
are never owned by this cleanup. Failed admission after allocation also queues
the owned arena; metadata locks perform no database or teleport work.

The route fixture injects return refusal after the fifth production death,
checks that both arena and saved return remain, then reconnects the owner. It
places a second character in the arena to prove the owner alone is returned and
the arena survives until empty. Final checks verify saved town location, return
acknowledgement, map/PK-list removal and unchanged earned soul reload.

This supersedes the prior missing-closure finding for tested in-process and
character-reconnect paths. Abrupt full-Host crash recovery, extended simultaneous
map admission and native client acceptance remain explicit validation gates.

Final validation: isolated build and full purse-review regression pass with
843 logged PASS assertions and zero FAIL assertions. Logs:
`obj/boss-rush-close-build.log` and `obj/boss-rush-close-tests.log` (ignored).
No live schema, process, client or deployment was changed.

## Five-boss victory through abrupt process exit (2026-10-01)

`career-rush-earn`, `career-rush-claim` and `career-rush-reload` pass against
private full-server SHA256
F020C8A2B264B6D420B62A5DFA5421387E11B8D5F6AC44BB8305FEEC1608049B.
The ordinary non-GM Extraction fixture continues with its previously earned
items/purse unchanged at admission. The test moves only the registration schedule
into its open window and invokes public Tick plus the installed Boss Rush NPC.
Production admission allocates a new arena; exactly one boss appears at each of
five stages. Controlled lethal ReceiveAttack calls traverse production deaths,
with TickInstance progressing waves; this is not normal attack calculation or
combat-balance evidence.

After the fifth death, HoldReturn deliberately blocks terminal return. The victory
run is unconsumed, the durable return is pending, and the character remains in the
owned arena. The fixture saves that state and exits the actual game process via
Environment.Exit without normal cleanup. PostgreSQL and the game then restart.
Ordinary RecoverPending restores Twin City and acknowledges the saved return;
it preserves the exact run terms, prior items and purses before payment.

The NPC claim grants the configured5000 CPs,14 RVPs,400,000 silver, one Perfection
Stone3009002 and one bound P7 soul (actual draw820074). The prior Extraction items
remain. Claim replay and repeated recovery produce nothing extra. A second full
game/database restart preserves exact item UIDs, purse values, progress/run rows
and consumed state. Loaded town position and return acknowledgement also pass.
`revora_rush_process_probe` stores the completed fingerprint; the separate retained
320-achievement career and its208 stored identities are unchanged.

This closes the previously untested full-process pending-return/unclaimed-victory
boundary for this controlled route. It does not establish lost COMMIT reply,
host power loss, natural schedule, simultaneous admission stress, authenticated
combat, native presentation or deployment. No production code/build changed and
no live process was restarted.


## Retained calculated combat and stored-item claim repair (2026-10-03)

The retained ordinary character4294907001 now enters through NPC900000010,
using its existing earned equipment, and defeats all five catalog-stat bosses
through `Attack.HandleAttack` melee calculation. The stages have1M/3M/6M/9M/12M
HP and required31/95/194/305/420 attacks respectively (1,045 total). Event ticks
advance each stage and save one unpaid victory. This is calculated combat,
not the earlier supplied-lethal-damage fixture. Registration time, proximity and
attack cooldown are controlled; AI retaliation and natural twelve-minute
survivability are not exercised and difficulty/native acceptance remain open.

The cold NPC claim exposed a real capacity defect: its database check counted
all position-zero items, including warehouse and wardrobe belongings. The
retained bag had25 items and15 free slots, but a two-item prize was refused.
The predicate now counts only position0, warehouse0 and non-wardrobe items,
matching other transactional reward services. Live and saved bag capacity checks,
owner balance/claim locks, atomic consumption and pending full-bag behavior remain.
The failed attempts did not consume the victory or change assets.

After the repair, that same saved victory grants exactly5,000 CPs,14 RVPs,
400,000 silver, one configured Perfection Stone and one bound P7 soul. The
fixture asserts the broader position-zero count exceeds40 while actual saved
bag count equals the live25; it does not delete storage to make the claim pass.
Immediate duplicate claim/recovery and a further cold reload preserve the exact
nested prior-career, inventories, balances and Boss Rush result/progress rows.

Fixture: `tools/CheckCandidateRetainedRush.cs`; phases
`career-retained-rush-{earn,claim,reload}`. Latest retained checkpoint:
`revora_retained_rush_probe`, wrapping the previous Elite combat snapshot.
Initial setup stopped before admission because the shared room helper rejects a
full40-slot bag. A normal zero-currency trade frees the first slot, and ordinary
trade/storage prepares25 bag items. All prior belongings are accounted for;
there was no reward deletion or replay of the earned fight during claim repair.

Earning executable: `F1FF87120645900CB1F7B288D1B9EB55519A3C124C037D7B9F383A9AAF616B9F`.
Fixed claim/reload executable:
`264AA20CC97083524A4ADDB0AC13F07DBBE0E4F93B34F030B6B6C49707E96A73`.
Private candidate only. This adds earning/reward evidence, not new achievement
credit, native acceptance or a live release. The unrelated vote pulse still logs
a PostgresException during this private run; it is not certified by these checks.

## Fresh five-boss earning and durable payout (2026-10-03)

- [x] Continue ordinary FreshRoadmap1 from its completed Extraction checkpoint.
  Store two existing items through the Ape City warehouse packet and enter
  Boss Rush through NPC900000010 using its earned equipment.
- [x] Defeat all five bosses through4,515 calculated melee attacks:
  99/334/750/1,296/2,036 against1M/3M/6M/9M/12M HP respectively. The completed
  event returns the character and saves one unconsumed victory.
- [x] After process restart, normal NPC claim pays5,000 CPs,14 RVPs,400,000 silver
  and two items including one bound catalog-P7. The result becomes consumed and
  the claim flag clears. Final balances are364,905 CPs,1,375 RVPs and11,097,612
  silver; bag40.
- [x] A further cold restart and duplicate claim/recovery preserve exact saved
  possessions, currencies, run/progress records and the nested prior career.

Fixture `tools/CheckCandidateFreshRush.cs` passes
`career-fresh-rush-{earn,claim,reload}` on the first run. Checkpoint:
`revora_fresh_rush_probe`. Private executable SHA-256:
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
The claim also checks saved bag occupancy separately from warehouse possessions.

Registration, proximity and attack cooldown are controlled. Retaliation,
natural timing/difficulty and authenticated/native combat remain open. This
batch changes private fixtures and evidence only; no production rebuild, live
restart, client patch or deployment occurred. The full compatible career remains
unfinished, and the post-Beast achievement export retains its dated scope.
