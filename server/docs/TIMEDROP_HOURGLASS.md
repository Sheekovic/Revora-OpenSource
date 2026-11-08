# TimeDrop hourglasses

M6.56b6, 2026-09-22. Server implementation; not deployed. Prizewell is not reopened.

## Issuance (closed)

Ordinary Twin City players cannot claim TimeDrop hourglasses 3007985 / 3007989 / 3007992.

NPC **Prizewell 900035** historically offered option 101 "Free Revora Points" to VIP 5+
and option 102 as the claim. `Npcs.GetDialog` reaches that switch only after
`TwinCityPlaza.HandleClosed`. Prizewell is in `ClosedDeskIds`, so every option
returns the closed-desk text and issues nothing.

Production boot also jails the actor with `ParkNpc(900035, 6000, 62, 70)` so the
database row cannot reload on Twin City. Vesper holds the live Revora Point stall.
`TimeDropSlayerToday` remains an in-memory flag; it is not a reachable daily
receipt while the desk is closed.

The closed switch still contains `Random.Next(1, 5)` (values 1..4):

| Roll | Item | Leftover use duration |
| --- | --- | --- |
| 1 or 3 | 3007989 | 3600s TimeDropSlayer + double-EXP |
| 2 or 4 | 3007985 | 1800s TimeDropSlayer + double-EXP |
| 5 | 3007992 | 7200s TimeDropSlayer + double-EXP |

Roll 5 never occurs. The super hourglass is **not** added to the economy.

## Leftover bag use

Hourglasses already in a bag can be used through the existing item-use path.
One transaction consumes one owned unit and writes both timers from durable
zero preimages.

| Item | TimeDropSlayer | DoubleExperienceTime |
| --- | --- | --- |
| 3007985 | 1800s | 1800s |
| 3007989 | 3600s | 3600s |
| 3007992 | 7200s | 7200s |

Use is refused if either timer is already active, so an existing RVP or
double-EXP value is not shortened or overwritten. Binding and zero-stack
historical singletons are preserved. The existing 1009 action-4 frame is
bounded to 32..128 total bytes.

Under the inventory owner lock, the transaction locks the character, checks
the TimeDrop and double-EXP preimages and the complete bag UID set, consumes
one source unit, writes `entities.timedropslayer` and `entities.doubleexptime`,
and inserts a `revora_timedrop_uses` receipt. Failed writes roll back together
and require reload. Direct `ItemHandler.UseItem` uses the same service. The
three legacy cases are fail-closed.

`TimeDropBalance` keeps the durable TimeDrop preimage separate from pending
countdown ticks, matching `DoubleExperienceBalance`. Countdown, autosave and
grants share the owner lock. Login refresh sends the existing timer without
extending or writing it. Ordinary monster kills observe the committed
TimeDrop field for Revora Point drops and the installed double-EXP timer for
kill EXP.

## Policy

This batch does not revive Prizewell, does not widen `Random.Next`, and does
not invent a replacement daily claim. Direct daily P7 is unchanged. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open pending Magnolia issuance
and remaining legacy branches.
