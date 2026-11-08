# Joint Canyon transporter terrain correction

## Problem and correction (2026-09-28)

Guild transporter9886 offers Joint Canyon for1,000 silver. Its ordinary service
used AdventureTravel.Landing, which required terrain identity1217 for map1217.
The installed map metadata and actual startup world use the reviewed fixed pair
1217/1075. Consequently the service rejected its valid destination before payment.

Landing now recognizes exactly1217/1075 beside the existing1219/1212 pair. It
still requires matching loaded world and metadata identities, owner0 and a valid
landing. No terrain, price, portal, client packet or map-table row changed.
The existing protocol evidence for this pair remains in
[travel research](TRAVEL_ACHIEVEMENT_RESEARCH.md#m645a-fixed-logical-map-aliases).

The earlier isolated guild-transporter test constructed1217 using the generic
World helper, masking the real alias mismatch. It now uses TravelAlias with
terrain1075. That fixture correction is included here; the broad historical
isolated suite was not rerun in this batch.

## Actual-process validation

`tools/Verify-ArenaProcess.ps1 -AdventureAliasOnly -ResetOwnedFixture` first
failed the successful-travel assertion on old executableB1ABF827, after confirming
both actual map and metadata had BaseID1075. The corrected isolated build passed
with existing legacy warnings. New private executable SHA256:
`C5791856C7459DF2F3AE5D96CE699A99AC554C853F8D28A47EA48E152155B0A2`.

Both game-process phases pass and the helper compiles with warnings as errors:

- An ordinary non-GM profile has a supplied2,000-silver purse and proximity on
  guild map1038. Temporarily substituting the destination's runtime terrain with
  1002 causes refusal with no charge or passage receipt.
- Restoring the actual1075 identity lets NPC9886 charge exactly1,000 silver,
  arrive at logical map1217 and finish one non-pending passage receipt. Both
  guild-transporter10117 and Joint Canyon visit10719 evidence are present.
- Game/database restart and canonical character load preserve the map, remaining
  1,000 silver, exact receipt and both achievement rows. A stale confirmation
  after departure changes none of those records.

The fixture supplies origin placement, proximity and funds; it does not prove
the full route from Twin City, socket UI or native rendering. No quest completion,
passage receipt or achievement is seeded. The owned clone was removed and private
PostgreSQL stopped. Native acceptance and live deployment remain pending.

## All four ordinary guild passages (2026-09-28)

The same two-phase process fixture now covers four separate ordinary unguilded
characters, starting on Twin City map1002. Each uses installed Guild Controller380
through production NPC dispatch to reach1038 before paying its transporter. This
supersedes the supplied-guild-origin limitation above; NPC proximity and each
2,000-silver starting purse remain supplied controls.

| Transporter | Destination | Additional earned visit identity |
| --- | --- | --- |
| 9884 | 1216 | 10712 |
| 9885 | 1015 | 10631 |
| 9886 | 1217, terrain1075 | 10719 |
| 9887 | 1001 | 10703 |

All four also earn guild-transporter10117. Each finishes with1,000 silver and one
non-pending passage receipt containing its own NPC/destination. A PostgreSQL/game
restart preserves each destination, purse, complete receipt and two selected
achievement rows. Stale confirmations after departure leave every snapshot
unchanged. The wrong-terrain/no-charge regression remains part of the1217 route.

Both phases pass on the sameC5791856 executable, with no new gameplay build or
source change. The helper compiles with warnings as errors. Four profiles are
used to isolate each service; this is not a combined320-achievement character,
authenticated socket journey or native movement test. Owned clone cleanup and
private PostgreSQL shutdown succeeded. Native/live gates remain pending.

## Adventure Islands onward route and return (2026-09-28)

The9884 visitor now continues from1216 through Old Explorer300655, eleven
installed portal crossings across1210/1211/1212, Grandpa300653 to logical1219
using terrain1212, and Boatman300654 back to Twin City. NPC and portal dispatch
use the actual startup world's entries; no helper-only world initialization or
new route is added. The trip after the paid conductor has no extra fare.

The helper supplies NPC/portal proximity, then makes one valid production walk
step before each sealed portal request. Each step is constrained to keep the
intended first matching portal within four tiles. This verifies accepted movement
and crossings at those locations, not a connected walking path between them;
the earlier isolated AdventureTests walking-route evidence retains that scope.
The first fixture attempt allowed a step outside portal range and was corrected.

The actual character earns10715/10716/10717/10718 through those accepted steps
and transitions, alongside10712 and10117 from the initial passage. The return
map, remaining1,000 silver, exact paid receipt and all six selected achievement
rows survive the second game/database process phase. A stale conductor request
does not charge again. The other three transporter profiles still pass their
existing destination/purse/achievement checks.

Both phases pass on the unchangedC5791856 gameplay executable; the expanded
helper compiles with warnings as errors. No combat, quest item collection,
authenticated socket route or native rendering is claimed. The driver cleaned
its owned clone and stopped private PostgreSQL. Live deployment remains pending.
