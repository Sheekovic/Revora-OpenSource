# Way of Heroes: sixty-minute EXP Potion

## Installed earning contract (2026-10-02)

Private `database/Slayer/stagegoal.txt` section `[2-6]` specifies stage 206,
TaskType 9, progress one, opening level 31 and one bound EXP Potion 723017. Its
instruction explicitly describes sixty minutes of double EXP.

The claim now requires an owned `revora_exp_potion_uses` receipt for item 723017
and 3,600 seconds. The existing consumer saves that receipt atomically with item
consumption and the double-EXP balance. Current timer balance alone is not proof;
a use of the ten-minute potion 722700 also does not satisfy this milestone.
Historic qualifying receipts remain valid after their bonus expires.

No new storage table, packet layout or consumer is introduced. Normal opening,
capacity, ownership/session and unique-claim protections remain. The reward stays
bound, plus zero, one item. The existing consumer prevents replacing an active
bonus: players retain the reward and can use it after their current bonus ends.
Other unfinished substages and main-stage prizes remain refused.

## Ordinary use, refusal and persistence

`tools/CheckCandidatePotionClaim.cs` adds `career-potion-claim-earn/reload`.
One ordinary non-GM character has supplied level 50, no rebirth and 50,000 CPs.
A supplied unrelated 3,600-second timer balance cannot claim the milestone.
Normal item use of a supplied ten-minute potion saves a 600-second receipt but
still cannot claim. The timer is then expired through the durable balance setter
under fixture control; this does not simulate real elapsed time.

A purchase of the full-hour potion through the actual CP shop charges its loaded
price. Normal item use consumes it, grants 3,600 seconds and records the qualifying
receipt. Eight concurrent claim requests yield exactly one bound single reward.
Two use requests while the bonus is active leave reward and snapshot unchanged.
After another controlled expiry, normal use/replay consumes the reward once and
grants another 3,600 seconds. There are exactly two hour-potion receipts, one
short-potion receipt and one non-legacy milestone claim.

The first earn run stopped at the active-bonus refusal check: a reused helper
incorrectly required consumption. Production correctly retained the reward.
The fixture now sends the two normal requests and checks nonconsumption. Recovery
requires the saved single claim, one use of each potion type, active 3,600-second
balance and exact bound single reward. Nothing was reset or re-awarded.
The resumed earn and fresh cold-reload phases passed.

Ordinary EntitySave and game/PostgreSQL stop/start preserve exact scoped purse,
inventory, Chi/Way, potion receipts and double-EXP balance. The loaded runtime
balance is also 3,600 seconds. Replaying the claim after restart changes nothing.
The isolated committed-source-overlay build passed with existing legacy warnings;
the harness compiled with warnings as errors. Private game SHA-256:
`1CCA4C96E24DC4804BB3D709E9E44483E50699BE1227C25E6D0EFD810B72F8BE`.
Logs: `arena-process-career-potion-claim-{earn,reload}.log`.
Both game closes completed and PostgreSQL stopped. Prior executable/manifest
preserved; unrelated owner changes were excluded from the build.

Native/authenticated presentation, real elapsed countdown, natural starting
level/funding, explicit interrupted-commit injection and live deployment remain
pending. Supplied initial values, controlled expiry and fixture recovery are
explicit limits. The retained main-career checkpoint is unchanged.
