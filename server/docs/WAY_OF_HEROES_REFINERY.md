# Way of Heroes refinery claims

Implemented and privately validated 2026-10-02; not deployed live.

Installed stage 808 requires equipped P1-or-higher refinery and awards one bound
3005127 pack (1,000 Chi on use). Stage 909 requires P5-or-higher and awards one
bound 3005128 pack (2,000 Chi on use). Both open at second rebirth level 121 and
accept temporary or permanent effects. Startup checks pin those definitions.

Claims now lock saved active-bank core equipment and read effects through the
existing `RefineryProgress.Read`/`Current` authority. This validates catalog
level/percentage and duplicate effect state. A live temporary effect masks its
permanent predecessor; after expiry the permanent effect becomes eligible again.
Simply finding a high-grade permanent row is not enough while a lower temporary
effect is active. A future temporary timestamp cannot qualify.

Items must match their loaded equipment identity and a valid slot, be permanent
single items outside storage/wardrobe and not suspicious. Effect compatibility
uses `RefineryProgress.Fits`. Bagged gear and inactive-bank equipment do not count.
The existing atomic prize/progress/unique-receipt transaction, saved opening gate,
owner/session checks, capacity guard and bound plus-0 output remain unchanged.
Chi pack use retains its atomic consume, wallet and receipt behavior.

## Composed process evidence

`tools/CheckCandidateRefineryClaims.cs` adds `career-refinery-claims-earn` and
`career-refinery-claims-reload`. A separate ordinary non-GM character receives
supplied level 140, second rebirth, initial CPs, boots and compatible P1/P5
materials. These inputs are explicit fixtures; their acquisition is not proved
by this batch. Existing refinery service evidence remains in
[Refinery Progress](REFINERY_PROGRESS.md).

The normal application request consumes P1 material. The claim refuses the
bagged effect and, after normal equipment, an expired effect under a controlled
clock. At current time, eight concurrent requests award stage 808 once. Actual
pack use grants exactly 1,000 Chi and replay cannot consume/grant again. The same
P1 state refuses stage 909.

The fixture applies P5 and buys stabilization stones through the ordinary CP
shop at loaded prices. The normal stabilization request consumes the selected
stones and saves the permanent P5 effect. The selected points round up to the
existing cost; excess selected points are consumed under the existing rule.
A newly applied temporary P1 effect masks that permanent P5, so stage 909 still
refuses. Advancing only the claim clock beyond temporary expiry admits stage 909
once under concurrent requests. Its pack grants 2,000 Chi once, for 3,000 total.
The clock is restored. Replaying the spent stabilization request changes nothing.

Game and PostgreSQL stop/restart, followed by a fresh process checking exact
scoped profile/purses, inventory/effects, progress, claim and consumer receipts.
Both claims remain replay-safe. The temporary overlay is still physically saved;
the controlled expiry proves selection logic, not real elapsed lifetime.

The isolated committed-source-overlay production build passed with existing
legacy warnings. The composed harness compiled with warnings as errors; both
phases passed on their first run. Private game SHA-256:
`26A187653CD1A509EF3F43F94B532BE716471F9392274A33BB640E8330248E6A`.
Logs: `arena-process-career-refinery-claims-{earn,reload}.log`. Both normal game
closes completed and PostgreSQL stopped. The previous private executable and
manifest were retained. Unrelated owner worktree edits remain excluded.

Native/authenticated acceptance, natural material/starting-fund earning, actual
elapsed expiry, alternate-bank gameplay and interrupted-commit recovery remain
open. Packets are in-process; item inputs and clocks are controlled. This adds no
main retained-career achievements and does not complete the overall roadmap.
