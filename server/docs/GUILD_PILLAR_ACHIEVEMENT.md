# Guild War pillar achievement (10232)

Achievement 10232 now follows the first accepted positive hit on the canonical
Guild War pillar, UID 810 on map 1038, during the owned Sunday battle. It does not
require a capture or a winning guild. Gate damage, zero damage, forged structures,
out-of-range or stale sessions, fallen/fake players and the holding guild remain
ineligible through the existing combat checks. The battle-start lower bound is
now checked as well as its exclusive ending boundary.

A first-hit receipt saves character, battle date, guild, structure, accepted damage
and UTC acceptance time. Saved guild membership and an active durable battle must
agree before the receipt and 10232 evidence commit. For a lethal capture, that
record shares the capture transaction: a rejected final strike cannot earn credit.
For a nonlethal first hit, the durable acceptance decision commits before runtime
score/HP projection. This is not durable pole-HP persistence; a crash between the
commit and projection retains the accepted-hit achievement and startup cancels the
interrupted battle under the existing rules. No receipt is invented from a loose
packet, old guild score or legacy achievement bits.

Hits acquire the session-install lease and owner lock before trying the battlefield
gate. Busy owners/battles reject the hit promptly. Achievement/title projection runs
after releasing the battlefield gate, while the owner lock remains held. A weak
per-entity cache avoids new credit transactions and title reconciliation on every
subsequent hit. Accepted receipts recover missing evidence during normal progression
loading; leaving a guild later does not erase historical credit. No currency,
item, schedule or capture reward is added or changed by this earning path.

## Validation

The isolated build and 374 guild season checks pass. Added checks cover the first
nonlethal attack, continued damage with exactly one first-hit receipt, the final
strike by a member whose guild did not win the damage contest, rejected capture
rollback, saved-membership mismatch, busy-owner refusal, receipt/evidence write
suppression, positive gate damage and both time boundaries. A fully loaded ordinary
fighter receives immediate achievement packet state, and a real character reload
repairs deleted evidence from its accepted receipt. Existing season/prize/purse,
guild leadership and activity regressions remain green.

The early combat fixture now saves its guild membership explicitly; its minimal
characters validate durable receipts. The later full-character purse journey
validates immediate achievement projection and recovery. These are server-handler
and disposable-database tests, not a native-client playthrough.

Remaining gates include genuine 6711 visual acceptance, fresh-Host crash acceptance,
extended movement/secondary-damage/disconnect and database-wait timing races, and
deployment. Initial receipt failure leaves the first hit unaccepted. The Guild
Beast earning path remains separate and incomplete.

Guild Beast follow-up: its separate daily earning route is now implemented and
isolated-tested; see [scope and pending gates](GUILD_BEAST_EVENT.md).
