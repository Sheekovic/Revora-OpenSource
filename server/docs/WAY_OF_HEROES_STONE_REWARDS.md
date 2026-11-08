# Way of Heroes: composition-stone metadata and two Super Gems

## Installed earning contract (2026-10-02)

Private `database/Slayer/stagegoal.txt` section `[6-3]` specifies stage 603,
TaskType 28, two embedded Super Gems, opening 1101 (first rebirth, level 101),
and one bound 730002 +2 stone. This uses the existing saved primary-equipped
socket predicate shared by stages 903/1006. Slots 1-12 are counted; inventory,
warehouse and alternate slots are excluded. Empty sockets do not count as gems.
This batch does not redefine that predicate or the separate full-set achievements.

The generic reward writer previously assigned and inserted Plus zero to every
reward. `EquipmentUpgrades.HandlePlus` calculates material contribution from
`Plus`: `DataHolder.StonePlusPoints(0)` is one, while `(2)` is forty. A nominal
730002 with Plus zero would therefore contribute too little. The reward builder
now derives Plus 1-8 for explicit stone identities 730001-730008 and saves that
same value in the atomic item insert. Other reward identities still receive
Plus zero. Only stage 603 is newly enabled by this batch; no existing stone
milestone claims are migrated because no stone milestone was previously owned
by this claim service.

The normal stage opening, ownership, inventory/session validation, bound output,
unique claim and transaction protections remain. Main-stage prizes and unrelated
substages stay unavailable.

## Ordinary handler and restart verification

`tools/CheckCandidateStoneClaim.cs` adds `career-stone-claim-earn/reload`.
One ordinary non-GM character has supplied level 140/second rebirth, 500,000 CPs
and bound plus-0 boots with two empty sockets. The test purchases two installed
Super Gems from the actual CP shop at its loaded prices and embeds each through
sealed packet 1027. It verifies material consumption and two saved embedding
receipts. No qualifying filled socket, claim or composition receipt is supplied.

The claim refuses before embedding, after only one Super Gem, and with the
fully embedded boots unequipped. Eight concurrent requests after equipping
produce exactly one bound, single +2 stone. Both runtime metadata and saved
`items.plus` equal two. Normal sealed composition packet 2036 consumes the stone
and produces plus-2 boots with zero remaining progress: precisely forty points.
Composition/claim replay changes nothing. After ordinary EntitySave, game and
PostgreSQL stop/start, a fresh process verifies the exact scoped purse, inventory,
Chi/Way state, embedding/composition receipts and claim snapshot. The boot result
and duplicate-claim protection persist.

The isolated committed-source-overlay build passed with existing legacy warnings;
the process harness compiled with warnings as errors. Earn/reload passed on their
first runs. Private executable SHA-256:
`18E882A67AE2EE63A0E94B3BEF6BD98E06642235FABB6FCD5063038AA423EBDD`.
Logs: `arena-process-career-stone-claim-{earn,reload}.log`.
Both game close paths completed and PostgreSQL stopped. Prior executable/manifest
preserved. Unrelated owner WIP was excluded from the build.

Native/authenticated display, natural level/rebirth/starting-CP/socket acquisition,
interrupted claims, all eligible slots and live deployment remain unverified.
The retained main-career checkpoint is unchanged. This does not prove all stone
grades through process tests: the exercised reward identity is 730002.

## Next installed composition milestones

Section `[4-3]` (403, task 16) asks for one equipment composition and pays three
bound Meteors. Section `[5-3]` (503, task 23) asks for an equipped +3 item and pays
another bound +2 stone. Their wording is concrete and the stone-output prerequisite
is now resolved. Their earning predicates and ordinary reward validation were
subsequently implemented in [the composition batch](WAY_OF_HEROES_COMPOSITION.md). Total-bonus stages 602/706/802/902/1007 still have the separate
slot-scope ambiguity documented in WAY_OF_HEROES_CHI.md.

## Later calculation correction (2026-10-02)

The original primary-only predicate described above has been superseded by
active-bank validation shared with stage707. Existing603 receipts remain intact.
See [eight-gem evidence and limits](WAY_OF_HEROES_EIGHT_GEMS.md).
