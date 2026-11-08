# Player revival achievement

6711 Achievement.dat SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`
identifies 10902 as reviving someone. Spell dispatch handles 1050 (XP revival)
and 1100 (mana revival), crediting the successful caster only.

PlayerRevival acquires the two character inventory locks without waiting and
rechecks canonical live caster/dead target, distinct player identities, matching
map instances and non-simulated actors. Busy or invalid requests return before
payment. Existing spell distance, Soul Shackle, learned-skill, mana/stamina,
XP-circle and event restrictions remain in the spell handler. A second caster
cannot obtain credit from an already revived target. No new casting class
allowlist is applied; legitimately inherited skills keep their existing rules.

After restoring HP and death state, the server saves one earned revival receipt
per caster. A separate transaction inserts 10902 and completes that receipt.
Progression reads, including login, recover pending receipts when achievement
projection failed. Suppressed evidence writes therefore no longer require another
revival. The target's transient revival and the initial receipt save remain
separate: if the initial database save itself fails, there is no durable receipt
and no automatic recovery guarantee. Neither mana nor revival is rolled back.

## Ordinary spell acquisition

Taoist Moon (NPC 10000, map 1004) already offered both lessons, but AddSpell only
changed memory until a later bulk save. Options 113 and 115 now use a transaction
that saves the progression image and skill before adding/sending the live skill.
Original eligibility is preserved: Water Taoist ranks 132?135, level 70 for 1100
and level 40 for 1050. The service checks the actual nearby trainer and canonical
logged-in progression state. Failure exposes no unsaved skill; reconnect/retry
works without duplicate rows. There is no new fee or GM/name allowlist.

## Validation and remaining gates

Build and 34 focused checks pass with `-PlayerRevivalOnly`. Coverage includes both
decoded spell handlers, caster-only credit, full reload, invalid targets without
mana consumption, competing casters, missing XP circle, evidence failure and
login recovery, ordinary trainer packet dispatch, immediate spell reload,
suppressed trainer writes and retry. One journey learns both spells through the
NPC, reloads them, causes an actual lethal ReceiveAttack player defeat and uses
the learned mana revival to earn credit. The disposable schema includes the
normal enemy-list persistence used by player death.

Character rank/level and the combat setup remain fixtures; this does not prove a
complete level-one progression route. Native packet encoding/UI, event-specific
revival policies and live deployment remain pending. The target health/mana
operation and receipt are not one atomic transaction spanning both players.

## Retained career recovery, training and revival (2026-10-02)

Following the paid Water132 reincarnation and apprentice admission, the same
retained main buys and consumes232 EXP balls through the ordinary shop/item
handlers. The fixture advances the consumable day after each ten uses; this
proves payment, daily-limit-compatible consumption and level110 recovery to the
saved140, not real elapsed levelling time. Female2001, the new senior relationship,
existing trainee and earlier nested career remain preserved. The normal Twin City
portal reaches the trainer map1004; Taoist10000 option113 teaches Revive1100.
A full game/database restart preserves the learned skill before combat.

Existing ordinary peer4294908909 is approached in private arena1005 with controlled
placement, PK mode and expired protection timers. Two calculated ranged attacks
from the retained bow kill it; no direct health assignment or injected lethal
damage is used. The learned decoded1100 handler restores it to full health and
earns main10902. Both return to Twin City and their state is saved. Another full
game/database restart preserves the exact nested career, peer items, spell and
revival receipt. A repeat cast against the living target adds no credit.

`career-retained-revival-{train,cast,reload}` all pass against unchanged candidate
SHA-256 `07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
Probe `revora_retained_revival_probe` finishes at step3. An initial fixture compile
error referenced the protection timer on Entity instead of GameState; it was
corrected before any phase executed. Existing peer qualifications and earlier
funding retain their fixture provenance. Authenticated/native spell encoding,
UI and live acceptance remain pending. No production build or deployment changed.
