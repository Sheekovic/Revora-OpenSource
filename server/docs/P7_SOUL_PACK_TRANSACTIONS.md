# Bound P7 soul pack opening

The Arena custom-prize review exposed two existing pack paths that consumed their
item before issuing an unbound P7 soul. Pack 3004247 (weapon soul) and 3004248
(equipment soul) now use `P7SoulPacks` through the ordinary item-use handler.
Both old switch bodies delegate to the owned handler; their unbound random-award
code is removed. Startup initializes the service with the other prize packs.

## Pool evidence and binding

All 17 weapon and 16 equipment IDs listed by the legacy branches exist in both
the installed server catalog and decoded 6711 client catalog. Every loaded server
entry has PurificationLevel 7. The pools retain those reviewed IDs; no broader
catalog-wide pool is inferred. Each entry is equally selectable. The legacy
exclusive upper limits made weapon 801218 FistofDemon and equipment 823062
TigerHeavyRing unreachable. They are now included alongside the other listed
rewards. This is a correction of Revora's own listed pool, not a verified retail
probability claim.

Every output is bound, whether its source pack is bound or unbound, as explicitly
required by the owner. This does not add P7 to any daily quest completion. Existing
pack acquisition and the Arena custom 100-win prize remain separate earning paths.

## Transaction and ordinary behavior

Opening requires the canonical ordinary player, exact inventory item and owner
lock. Locked, suspicious, stored, wardrobe and timed items cannot be consumed.
One unit is removed or its stack decremented in the same transaction as the bound
soul insert and an opening receipt. The receipt saves source identity, pre-stack,
source binding, result identity and UTC time. Failed consume, random draw, item
insert or receipt leaves the source and rewards unchanged. Post-commit errors
fence the session for reload.

Singletons, including legacy zero-stack singletons, reuse their freed inventory
slot. A full bag preserves a stacked pack until there is room for its output.
A saved one-second per-player/pack cooldown prevents concurrent stacked requests
and reconnect replay from immediately spending another unit. Later deliberate
requests can use the next unit. Results and binding survive production reload.

The isolated build, **146 pack checks** and **15 packet regression checks** pass.
Every listed reward is exercised from both bound and unbound source packs, with
production character/item reload afterward. Further checks cover simultaneous
stack use, saved cooldown/reconnect, full bag, zero-stack singleton, failure
injection and normal reconnect retry. Logs are ignored under
`obj/chi-refill-validation/p7-soul-pack-tests.log` and
`p7-pack-packet-regression.log`. Validated build SHA-256:
`0967ba610020c4b13081c7635675e183e6224627afb188abb17c0a63ab81f3e1`.

This is source integration, not a live deployment or native client playthrough.
The fixtures inject the source packs; scrap-to-pack acquisition is not proved by
these opening tests. Legacy scrap aggregation and refinery/stone conversions
still require separate atomic treatment before claiming the entire custom-prize
journey complete. Arena's custom currency/item entitlement and coordinator are
also unfinished. No production process, database or installed client changed.
