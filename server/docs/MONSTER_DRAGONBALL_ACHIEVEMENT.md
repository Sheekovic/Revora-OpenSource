# Monster DragonBall pickup achievement

The installed 6711 Achievement.dat identifies 10523 as looting a DragonBall while
hunting monsters. Catalog SHA-256:
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
10524 is the separate Meteor pickup identity and remains unimplemented.

SnowBansheeRewards now records 10523 in the same transaction that claims a
saved DragonBall loot entitlement and inserts its inventory item. Its existing
registered floor-object provenance, saved encounter/slot, owner protection,
expiry, player eligibility and inventory checks remain authoritative. Merely
owning a DragonBall, copying floor fields, setting MobDropped, receiving a
shop/pack reward, or killing the boss without collecting a DragonBall does not
create this evidence. Post-commit progression refresh exposes it immediately.

This supplies a real ordinary-player earning route without changing drop odds,
reward binding or quantities. It does not certify every legacy monster drop
path. Generic legacy floor pickup still needs a separate transactional review;
it removes the floor item before confirming inventory persistence. No new
achievement hook was attached to that unsafe path.

Validation uses the existing isolated Debug/x86 build, which includes this
source change. All 43 Snow Banshee reward checks and 81 encounter checks pass.
Fifteen added reward assertions cover copied-origin rejection, suppressed item/
loot-claim/evidence writes, retained floor loot, concurrent retry and full
character reload plus replay. Three added encounter assertions cover the actual
registered combat victory, no early credit, two earned DragonBall pickups with
one achievement record, and full production reload. Combat time, damage and RNG
are controlled fixtures; no native gameplay/balance acceptance is claimed.

Five inventory audit tests pass. Source producer count is 279/337, with 58
identities still missing; source presence is not a playable completion count.
No live deployment, schema mutation or client patch occurred. The isolated build
contains unrelated owner edits.
