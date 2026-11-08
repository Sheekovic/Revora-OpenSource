# Dedicated equipment sockets

Action 43 now uses an owned transaction instead of separate material deletion,
socket saving and failure-refund writes. The selected material UIDs must match
exactly; merely carrying enough DragonBalls elsewhere cannot pay with junk.

| Equipment | Socket | Payment |
|---|---|---|
| Weapon | First | 1 DragonBall |
| Weapon | Second | 5 DragonBalls |
| Other supported equipment | First | 12 DragonBalls |
| Other supported equipment | Second | 1 Tough Drill, chance |
| Other supported equipment | Second | 7 Star Drills, guaranteed |

These recipes follow the [official socket guide](https://wp.99.com/guide/items/socketed.shtml).
The guide does not publish Tough Drill odds. Revora retains its existing 379/699
success rate. Failure returns one Star Drill with the consumed drill's binding.
One unit is consumed per distinct selected UID; repeated UIDs are rejected.
Bound materials cannot upgrade unbound equipment. Rentals, locked equipment,
trading, fake sessions and stale ownership are rejected.

The approved DX9 executable's static action-43 sender/builders establish the
variable material list; see PROTOCOL_RESEARCH.md. No new native acceptance is
claimed. Packet 1009 action 43 delegates to EquipmentSockets. Startup creates
revora_equipment_sockets. It records target, selected UIDs, stacks, before/after
sockets, success and any refund UID in the payment transaction. Achievements
10425/10426 commit with successful socket opening. Postcommit projection failures
require a full reload; the paid result survives without another consumption.

Validation: 49 focused checks exercise exact recipes, stacked consumption,
concurrency, malformed service payloads, mixed/junk payments, binding, locks,
rentals, drill success/refund, suppressed material/target/receipt/achievement/
refund writes, rollback and full character reload. Valid requests use the normal
packet dispatcher. Malformed payloads call the service directly because the
outer dispatcher correctly disconnects malformed sessions.

Inventory boundary coverage includes a full 40-slot bag: a singleton drill can
free its own refund slot, while a stacked drill requires one free slot before
any charge. Both cases preserve the refund, remaining stack and receipt on
production reload. Ordinary equip into slots 8 and 28 precedes twelve-material
socket payment; both positions and sockets survive the production item loader.
Saved warehouse mismatches for either target or payment reject the transaction
and fence the stale session without consuming the DragonBall.

Tough Drill 1200005 is listed in the installed CP-mall stock. The generalized CP
shop purchase service is currently uncommitted work outside this socket batch;
its presence is not an earned-drill certificate. ToughDrillPack 721223 has no
confirmed reachable producer (see NESTED_GEAR_PACKS.md). No new drill producer
or price is invented by these tests.

The Snow Banshee regression additionally joins six consumed DragonBall UIDs to
actual boss loot pickups, opens both sockets on an ordinary purchased weapon,
and verifies exact remaining loot and silver after production reload. Boss time,
lethal damage and loot draws are controlled fixtures; this does not measure live
earning time or native UI behavior. The older forty forced quality failures test
checks progress boundaries and is not a practical guaranteed earning route.

The Way of Heroes socket fixture now uses the native variable-length frame and
the same service; all 2,809 checks in that suite pass. Three source-audit tests
also pass. The producer census remains 288/337; this adds a practical path for
existing socket achievements, not new catalog IDs.

Live deployment, native UI acceptance, and earned nonweapon drill acquisition
remain pending. The broader collection suite currently stops on changed Euro
jersey stock before reaching sockets; focused service and boss-route validation
are reported separately.
