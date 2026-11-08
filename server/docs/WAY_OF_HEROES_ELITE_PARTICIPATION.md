# Way of Heroes: Elite PK participation

## Contract and evidence (2026-10-02)

The private installed `database/Slayer/stagegoal.txt` section `[4-5]` specifies
stage 405, task 17, progress 1, opening level 101, and one bound EXP Ball 723700.
Its instruction is to compete in Elite PK once. This is separate from the
Top 8/Top 3/Champion milestones restored earlier.

Revora uses a completed tournament with a saved contested bout to prove this
participation. Either fighter qualifies, including the loser. The run must be
complete with champion/uncontested outcome, and the character's entry must share
its cycle. A bout must have two distinct nonzero fighters, a winner among those
fighters, defeat/timeout outcome and nonnegative damage with at least one side
positive. Signup alone, a bye, no-show, idle match, pending or cancelled run does
not qualify. This is the documented Revora earning definition; no claim is made
that original Conquer used the same database predicate or completion timing.

`WayOfHeroesClaims` reads the result records inside its existing owner-locked
claim transaction. It retains the installed opening gate, bound plus-0 reward,
capacity and session checks, unique claim receipt and atomic progress/item save.
No new packet layout or catalog item is introduced. Main-stage prizes and
unimplemented substages remain refused.

## Ordinary earning and cold restart

`tools/CheckCandidateEliteParticipation.cs` adds
`career-elite-participation-earn` and `career-elite-participation-reload`.
Two ordinary non-GM characters have supplied level 130 and no rebirth. Claims
before signup and during registration produce no items. Normal Envoy signup,
actual event scheduling, ReceiveAttack damage and controlled Entity.Die finish
a two-player bracket. No result, placement or claim is inserted by fixture SQL.

Both winner and loser send eight concurrent claim requests. Each receives exactly
one bound, plus-0, single EXP Ball. Normal item use awards experience and consumes
it once; replay cannot consume it again. Each saved daily EXP Ball allowance is
one. Scoped player purse/inventory/Chi/Way state, experience/level/allowance and
all selected tournament rows match after game and PostgreSQL stop/restart.
Replaying the milestone after restart changes nothing.

The first process run stopped after using the first reward because its assertion
cast PostgreSQL integer `used` to boxed Int64. The fixture now converts it to
Int32. Recovery reused the existing run, exact claim, empty bag, earned experience
and allowance one; no earning/payment/reward records were reset. The resumed earn
phase and a fresh cold-reload phase passed. The second player followed the full
claim/use/replay path in the successful phase.

The isolated committed-source-overlay build passed with existing legacy warnings;
the process harness compiled with warnings as errors. Private game SHA-256:
`31989B04273B605ECC3B062DC4072EAE116BBCA3C0C9716E19EEBA10B10B4CAB`.
Logs: `arena-process-career-elite-participation-{earn,reload}.log`.
Both game closes completed and PostgreSQL stopped. Previous executable/manifest
retained; unrelated owner worktree changes were excluded from the build.

Native/authenticated UI, natural combat/calendar/starting-level earning, all
bracket levels, interrupted claim commits and live deployment remain pending.
The cancelled/bye/no-show/idle exclusions were source-reviewed, not exercised as
separate process tournaments in this batch. Existing EXP Ball level/allowance
limits remain: a high-level player may need to retain the awarded ball until it
can be used. This does not change the retained main-career checkpoint.

## Follow-on gaps identified

Early equipment-quality, promotion and activity milestones remain unimplemented.
Do not activate them from packet assertions or cached client progress.
Stages paying composition stones need reward-specific metadata before activation:
the generic Way of Heroes writer currently persists plus zero, while the owned
`EquipmentUpgrades` composition path reads material `Plus` for contribution.
In particular, stage 603 pays stone 730002. Reusing the generic plus-zero output
without checking the actual consumer would create an underpowered
reward (one point instead of the intended forty for a +2 stone). This review has not enabled that stage or changed its reward.
