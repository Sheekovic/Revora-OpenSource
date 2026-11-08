# Snow Banshee public location and persistence prerequisite

Integration follow-up: [the owned public encounter](SNOW_BANSHEE_ENCOUNTER.md)
now connects this storage to combat, startup/ticks, rewards and spirit credit.
The inactive status below describes the earlier prerequisite batch; native/live
acceptance is still open.

Follow-up: [the reward participant](SNOW_BANSHEE_REWARDS.md) now implements
weighted loot, Study credit and atomic native pickup, with separate fixture
validation. Both participants still await owned encounter activation.

This is an **inactive storage participant**, not a completed earning route.
No startup hook, actor, combat, reward grant or spirit source is enabled by this
batch. The existing Grotto NPC and Golden War paths are unchanged. The distinct
Purple Banshee house summon remains dormant and preserves its summoning item.

## Site evidence

The [official guide](https://co.99.com/guide/monsters/snow_banshee.shtml) places
Snow Banshee at Frozen Grotto 2 (341,362). In the loaded Revora map 1927 this tile,
and every tile in its surrounding 16-tile square, is blocked. Use **(370,365)**
as an explicit Revora location adjustment: it has a walkable twelve-tile radius
and connects to the Grotto Envoy's normal (388,628) landing. It is not claimed to
match the guide's exact location.

Reviewed terrain SHA-256:
`5f4b3f51a8a022c217cf553909d96583d05d489953128a4fbf599b088611cbce`.
Initialization checks that hash and the canonical public map identity; ownership,
terrain aliases, invalid coordinates and positions beyond a 24-tile leash are
rejected. The fixture separately checks the complete attack clearing. Runtime
actor placement still needs occupancy checks before activation.

A bounded read-only catalog query on 2026-09-21 found no monster spawn rows for
families 4171/4172 and no NPCs in map 1927's (350..390,345..385) rectangle. The
Revora-owned inspector SHA-256 was
`69A74713EB2C4E77CD9B4CD1865A651E558839DE1494065CB554BCEABA97635B`.
This does not rule out scripted summons or later placement changes.

## Explicit Revora lifecycle policy

The guide says every half hour but does not define the timer's anchor. Revora's
candidate policy is **30 minutes after a committed defeat**, with one surviving
boss at a time. A fresh installation is immediately due; later initialization
preserves the stored timer. An undefeated boss survives restart at its last
checkpoint, without creating catch-up spawns. Candidate maximum health is
100,000,000, consistent with the local IceQueen catalog, not verified retail HP.

The schedule row and a unique active-run index serialize creation. Each actor
generation receives a nonempty lease: reconstruction retains the run and HP but
invalidates the previous generation's writes. Increasing checkpoint sequence
numbers reject late health writes while allowing legitimate newer healing.
Only the victory transaction may persist zero HP.

Victory records the killer and frozen participant IDs, clears the active pointer
and schedules the next spawn in its caller's transaction. The future coordinator
must authorize those participants and commit every reward and spirit receipt in
that transaction before retiring the actor. Storage is not authorization to grant
rewards. A missing lifecycle lock, changed world context, failed receipt write or
transaction rollback leaves no partial committed victory. Duplicate/stale deaths
cannot reopen a completed run or mutate its successor.

The legacy PostgreSQL driver reads timestamptz in local machine time. Schedule
reads explicitly select UTC wall time before attaching UTC kind; the cooldown
test caught an early-respawn error in the initial implementation. Writes require
UTC. No player-local or Pacific calendar conversion controls this interval.

## Validation and remaining work

The isolated build, **33 Snow Banshee storage checks** and **106 travel checks**
pass. Storage coverage includes concurrent acquisition and death callbacks,
reconstruction, stale generations/checkpoints, healing, cap/terrain validation,
reward-write failure, precommit context loss, rollback, initialization during an
active run and cooldown, and the exact thirty-minute boundary. Reward receipts
in this fixture are synthetic; real boss rewards are not implemented here.

The ordinary non-GM travel fixture uses the existing Grotto Envoy dialogue to
reach floor 2 and the production walking method to reach (370,365), following
walkable tiles without diagonal corner cutting. This is server travel evidence,
not genuine client movement or a played boss encounter.

Ignored logs: `obj/chi-refill-validation/snow-banshee-store-tests.log` and
`snow-banshee-travel-tests.log`. Only a disposable database schema was written.
No installed client, live server or production player state was changed.

Still required: registered actor and damage/death ownership; occupancy-safe
recovery and checkpoint scheduling; seven reviewed skill behaviors and freeze
cleanup; persisted weighted loot, Study Points and owned 1,000-spirit credit;
replacement of only the corresponding legacy Grotto summon path; ordinary-player
combat/pickup/reload/concurrency checks; genuine client HP/skill/movement and live
Host restart/rollback acceptance. Event P7 drops must be bound. Daily completion
P7 stays removed; one-time quest rewards and custom packs keep their bound P7.
