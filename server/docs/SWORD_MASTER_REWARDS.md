# Sword Master durable rewards

## Contract and evidence

The [official guide](https://co.99.com/guide/monsters/sword_master.shtml)
documents 20 Study Points per teammate and two drops, each with a 60% P1-P3
artifact / 40% weapon-accessory split. The implementation enumerates valid
P1-P3 catalog entries and checks the random branch and pool index bounds.

Revora explicitly chooses the same reviewed seven-day accessory catalog used
by its Terato encounter: 350001, 360001, 370001 and 380001. The original guide
does not establish these exact accessory identities or their lifetime. A third,
guaranteed random **bound P7 soul** implements the owner's valuable event-reward
policy. It is a boss reward, not a direct daily Soul Ball completion reward.

## Transaction and recovery

`SwordMasterRewards.Record` participates in the owned combat coordinator's
victory transaction. It requires the canonical killer's item lock, an exact saved
victory with matching killer, and a nonduplicated recipient subset of the frozen
admission party. It refuses an active or failed attempt. Study entitlements and
all three loot records commit together with the caller's victory; duplicate or
suppressed writes roll everything back. This method cannot prove a combat death
and is not a replacement for registered encounter ownership.

Study claims serialize on the ordinary player's lock and saved entity row, check
saved versus cached balances, update the receipt and balance together, and record
normal Study Point achievement evidence. Overflow keeps the entitlement pending.
Disconnected players can receive unpaid entitlements after reconnect; no GM gate
or owner allowlist exists.

Ground drops have persisted identities, binding, protection and two-minute expiry.
Reconstruction checks both the saved house reservation/terrain/level/owner and the
live canonical map metadata. Pickup uses the existing sealed packet 1101 path and
ItemHandler path, validates the exact floor object, owned inventory capacity and
item provenance, and commits the collector receipt with item insertion. The P7
binding is checked on the floor and saved in the item row. Accessory lifetime begins
at collection. Recovery does not redraw rewards or recreate collected items.

## Availability and remaining work

The [owned encounter](SWORD_MASTER_ENCOUNTER.md) now invokes this participant
from registered lethal combat and combines victory with 1,000 Soul Ball spirits.
Candidate startup/timer integration and ordinary item confirmation are connected.
The original storage fixture below still deliberately supplies a victory to isolate
reward behavior; the newer encounter fixture proves the server combat route.

Native animations/long HP bar, composed level-100 client tooltip, played Arena
Honor acquisition and live Host restart/rollback acceptance remain open. The
candidate has not been deployed. No installed client or live database was changed.

## Validation

Isolated legacy x86 build and `Verify-PotencyHunt.ps1 -HousingOnly`: 557 combined
checks pass. This includes existing housing/admission prerequisites, not 557 new
independent reward tests. Added coverage checks all 100 random branch outcomes,
invalid draws, active-run/foreign-recipient refusal, duplicate victory rollback,
frozen-team payout, concurrent native pickups, malformed frames, full bags,
seven-day accessories, bound P7 persistence, suppressed entitlement/item/balance
writes, overflow recovery, changed live house ownership and late/expired pickup.
Production reload retains items and paid Study Points. The previous test failure
was a fixture mutating a copied metadata struct; the corrected fixture writes the
changed value into the actual map registry before checking refusal.

Recovery selects loaded houses and omits already-rendered loot from each bounded
256-row batch so rendered drops do not repeatedly occupy later batches.
