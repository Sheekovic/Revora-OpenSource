# Monster Meteor loot achievement

6711 Achievement.dat (SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`)
identifies 10524 as collecting a Meteor while hunting monsters. Possession,
purchases and opened packs do not establish monster-loot origin.

MeteorDrops adds a Revora 1% ordinary monster drop in maps 1002, 1011, 1020,
1000 and 1015. `Config.MeteorDropBasisPoints = 100` is the selected Revora rate,
not a recovered original drop probability. The old DropItemTable contains a
Meteor row, but the source audit found no production consumer of that list.
The change does not alter named boss reward pools or existing quest rewards.

Eligible deaths require a canonical ordinary killer, the exact registered dead
monster and base map, matching monster metadata, and no companion, boss, guard
or reviver. One roll is allowed for each observed death stamp. The producer
records the exact item/floor identities, location and owner in server memory.
Player-dropped or fabricated floor items do not acquire this origin.

Pickup respects the original owner/team window, map, exact coordinates, expiry,
ordinary-player state and bag capacity. Under player and floor locks, it saves
the unbound Meteor, pickup receipt and achievement 10524 in one transaction.
Only after commit does it remove the floor item and refresh inventory/progress.
Failure fences the player for reload and retains the unclaimed floor object;
recovery can retry until normal expiry. Uncollected floor drops are not durable
across a server restart. Collected items and achievement receipts are durable.

Build and 18 focused checks pass using `-MeteorDropsOnly`: duplicate death rolls,
concurrent pickup packets, remote/other-owner/full-bag/expired rejection,
non-loot possession, production reload, each suppressed item/receipt/achievement
insert with rollback and retry, rate boundary and guard exclusion. A final case
uses actual ReceiveAttack lethal damage through MonsterInformation.Drop and the
native-layout FloorItem.Process pickup route. Randomness is forced for the test;
it does not measure observed live drop frequency or genuine client rendering.

The managed pickup is shared by manual and automatic pickup entry points. Native
6711 acceptance, real auto-hunt interaction and deployment remain pending. No
client patch is required by the new server-side earning rule itself.
