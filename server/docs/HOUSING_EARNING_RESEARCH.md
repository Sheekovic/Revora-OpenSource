# Housing earning restoration

Current implementation: [certificate construction and travel](HOUSING_SERVICE.md)
now supersedes the inactive participant checkpoint below. The full earning and
native acceptance contract remains unfinished.

2026-09-20, inspected baseline `187aeee`. Research and implementation contract;
no new housing service or achievement producer is enabled by this document.

## Confirmed reward and asset identities

The installed 6711 achievement catalog has five house-building requirements:
10102, 10103, 10104, 10105 and 10122 correspond to levels 1 through 5. Furniture
placement is a separate requirement, 10101. None had a trusted producer at the
inspected baseline; the later certificate service supplies the five building
producers. Owning a legacy row alone does not prove paid
construction or furniture placement.

Read-only catalog inspection used the existing `item_rows` decoder in
`tools/audit_warrior_epic.py`, including its exact encryption round trip.
Both installed catalogs agree on the following identities. Map IDs below are
the existing server's terrain choices, not newly verified native bindings.

| Target level | Required certificate identity | Catalog name | Existing terrain |
| --- | --- | --- | --- |
| 1 | 721170 | HousePermit | 1098 |
| 2 | 721174 | UpgradeCert. | 1099 |
| 3 | 711198 | UpgradeCert.(Class3) | 2080 |
| 4 | 711200 | UpgradeCert.(Class4) | 1765 |
| 5 | 711553 | UpgradeCert.(Class5) | 3024 |

SHA-256 inputs:

- Client `ini/Achievement.dat`: `df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
- Client `ini/itemtype.dat`: `d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`.
- Server `database/itemtype.txt`: `594ffe5f252c9445a6087004372519a6f150f089c7bff42d4ec30400911a0e88`.

All five server terrain files exist. Their hashes, in level order:

1. `db5908f02e7a5ac33859e180f69275085768c864ae1cdce70415a6f7e9424014`
2. `9619a90984e1fd2f959ce89f918075353a5bc5839cbdd6d4eef2a1b05c9a87c4`
3. `0140e24af7892071ce345c25c153ec8db8a94a801f5830b2733ce4d2323cdfcd`
4. `c33269e36ab6dd379b472271ddb4546ffd2d50ce1b586c52fd247c9161f876c4`
5. `bbb2abb9cf4f023af233c4a3df078de59e3e3505dd5b655b061977011172bae8`

## Published construction requirements

The official [housing changes guide](https://co.99.com/guide/guides/house_change.shtml)
publishes these alternatives:

| Level | Gem/silver option | Certificate/silver option |
| --- | --- | --- |
| 1 | 6 refined gems + 1 million silver | Permit + 300,000 silver |
| 2 | 7 refined gems + 2 million silver | Certificate + 500,000 silver |
| 3 | 8 refined gems + 3 million silver | Class 3 certificate + 2 million silver |
| 4 | 2 super gems + 4 million silver | Class 4 certificate + 5 million silver |
| 5 | 40 super gems + 80 million silver | Class 5 certificate, or the separate material installment route |

That guide also specifies furniture capacities of 0, 8, 9, 10 and 12 and item-box
capacities of 1, 2, 2, 2 and 2. These are published rules, not current Revora
behavior. Its class-3 wording says permit; installed 6711 calls the item a
certificate. Native selection/recognition remains to be verified.

The official [level-2 upgrade guide](https://co.99.com/guide/guides/upgradehouse.shtml)
describes an earned certificate chain: 12 copper ores per copper voucher,
10 copper vouchers per ore voucher, then 10 ore vouchers per certificate.
It requires 500,000 silver and describes completion after maintenance.

The [PC level-5 guide](https://co.99.com/guide/guides/lv5house.shtml) describes
installments totaling 5 Dragon Balls, 20 Lucky Amulets, 40 Euxenite Ores,
20 Meteor Scrolls and 30 super gems. It names Thunder, Glory and Tortoise as
unneeded and confirms the other families suffice, rather than unambiguously
proving rejection of the named three in every edition. It requires a level-4
home and warns owners to remove
furniture before upgrading. Do not infer the installment exception applies to
every gem option without additional evidence.

The [installment evidence follow-up](HOUSING_INSTALLMENT_RESEARCH.md) records
the current identities, missing manager/showroom, reachable server-floor approach
and an unresolved local Lucky Amulet price mismatch (client 5 CPs, server 75).
It also distinguishes confirmed totals from unspecified order/binding rules.

## Implementation defects at the inspected baseline

`Npcs.cs` HouseAdmin 30156 currently consumes certificates without the published
silver payments. Its CP certificate purchases add an item before deducting CPs,
do not check the Add result, and use `>` rather than `>=`. The configured CP
shop is an existing Revora economy choice; these guides do not verify its prices.

`Database/HouseTable.cs` derives each map ID with `(ushort)client.Entity.UID`.
Owners whose IDs differ by 65,536 collide; an owner whose low word equals a
public map also collides. `Load` can remove a map using that value, while
`Upgrade` removes metadata and the runtime map before saving the new level.
Certificate debit, persistent construction, map publication and furniture
deletion are separate operations. Failure can therefore lose payment or leave
runtime state inconsistent. These are source findings, not claims that a
particular live character has suffered loss.

Furniture is loaded by map ID in `Game/Map.cs`, and upgrades delete it by owner
name. `HouseTable.SaveFurniture` accepts a deserialized NPC, but a bounded source
search found no caller. Neither that method nor stored furniture alone proves a
working ordinary placement journey. Placement packet identity and item ownership
must be established before attaching 10101.

## Implementation sequence and acceptance

1. Audit installed House Admin, certificate acquisition actors, database schema,
   house/furniture ownership and client terrain bindings. Keep any live metadata
   reads read-only and report no account or credential data. Preserve ambiguous
   legacy records for reconciliation instead of guessing their owner.
2. Replace truncated-owner map identity with explicit ownership and collision-safe
   runtime allocation coordinated with the existing dynamic-map allocator.
   Allocation must check static maps and concurrent private worlds. Reconnect
   and saved private locations need recovery; a process-local counter alone is
   insufficient. Test owners separated by 65,536 and public-map low-word matches.
3. Implement canonical ordinary-player NPC admission and sequential construction.
   Commit exact material/silver payment, house level and achievement together.
   Publish maps after commit and recover publication failures without charging
   again. Protect occupants and stored items; require furniture removal before
   upgrading rather than silently deleting possessions.
4. Restore earned permit/voucher paths and protect CP purchases transactionally.
   Validate both exact-balance and full-bag purchases, duplicate requests, failed
   writes, binding, concurrent owner sessions and production reload. Separately
   implement the original gem and level-5 installment alternatives; a certificate
   shortcut alone does not complete the published housing system.
5. Verify actual furniture purchase, owned placement/movement/removal, capacities,
   item boxes and visit permissions before awarding 10101. Validate all five
   native house interiors, entry/exit, reload and achievement presentation in
   genuine 6711. Maintenance timing and any explicit Revora edition changes must
   be recorded rather than silently advertised as original behavior.

No implementation, native acceptance, historical backfill or deployment is
claimed complete. This contract closes research uncertainty about the five
certificate identities and published costs; it does not close housing earning.

## Durable identity participant (2026-09-20)

A read-only database inspection found zero rows in the live `house` table.
Its columns are id, map, type, weather, owner, houselevel and name. Furniture
stores id, nameowner, type, lookface, mapid, cellx and celly. Installed actors:
House Admin 30156 at 1002 (331,536), Stoneman 30160 at 1036 (184,103), and House
Agent 30157 at 5000 (83,95). These are Revora's installed locations, not the
official guide coordinates. The inspector reads protected configuration without
printing it and runs a read-only transaction; its x64 binary SHA-256 is
`94EC47AE152D86947D1302C9B3E0ECBF0B450AD7D00E40D885A3A155E3E90D2E`.

`Database/HouseMapRegistry.cs` now implements a durable reservation participant.
It reserves IDs 60000-65534, disjoint from the event allocator's 11000-59999
range. Static terrain, runtime maps and map metadata consume otherwise available
capacity. A unique database map ID, per-owner primary key and transaction-scoped
allocation lock prevent concurrent owners from taking the same identity. Owner
locking precedes the allocation lock to match future construction transactions.
The bounded capacity is 5,535 before static reservations; exhaustion rejects new
reservations and preserves existing owners.

Reservation requires an existing saved character and the caller's transaction.
It neither commits independently nor creates a house entitlement. Payment and
construction callers must reserve in the same transaction as their complete
state change. A suppressed insertion throws, allowing all participants to roll
back. Existing reservations are stable across processes; conflicting static or
foreign runtime ownership fails for reconciliation instead of silently moving
a saved house. Static catalogs must be loaded before new allocation, and future
runtime publication must use the same durable owner binding.

The isolated build and `tests/PotencyHunt/Verify-HouseMapRegistry.ps1` pass 14
checks. Tests use only a newly created `revora_house_validation` schema, refuse
an existing schema, and delete only their own schema afterward. Coverage includes
three owners separated by 65,536; 12 simultaneous requests for one owner;
32 simultaneous distinct owners; rollback/reuse; missing owners; static/runtime
conflicts; a suppressed insert rolling back a participating synthetic payment;
full capacity; and a separate child process recovering saved reservations.
The process test is storage recovery, not a full Host restart or client journey.

This participant has **no production initialization or player caller yet**.
Legacy `HouseTable` and its NPC flow remain unchanged. No live housing schema,
payment, map or achievement was modified. M6.64b remains open until ownership,
construction, earning, furniture and native acceptance are connected and tested.

## Later earning prerequisites (2026-09-20, `fc50215`)

The [class-3/class-4 guide](https://co.99.com/guide/guides/lv34house.shtml)
independently agrees with the certificate/silver and gem/silver alternatives
above. Installed client and server catalogs additionally agree on 711199
Class3HouseVoucher and 711201 Class4HouseVoucher. Their client tooltips each
require ten vouchers at House Agent to obtain 711198/711200 respectively.
Voucher acquisition is not established by those tooltips or the published guide;
do not invent an ore recipe for them. Both catalogs also identify 723087 as
LuckyAmulet for the future level-5 installment contract.

The [official PC gem-composition guide](https://co.99.com/guide/guides/gemcomposing.shtml)
requires fifteen same-type normal gems plus 10,000 silver for refined, and
fifteen refined gems plus 800,000 silver for super. Refined Tortoise to super
instead costs 1,000,000 silver. The separate mixed-gem Tortoise recipe lists
several possible outcomes without publishing their odds.

Source inspection found a prerequisite defect in the compiled `ItemUsage.cs`
GemCompose branch: it ignores the material-removal result, adds the next item ID
without a recipe whitelist and omits the published silver fee. Its ToristSuper
branch also ignores seven individual removal results. These are source findings,
not claims of observed production abuse or native wire verification. Replace them
with bounded, owned transactions and validate earned gem composition before
claiming a complete gem-funded house route. Preserve binding, exact payment,
failure rollback and ordinary-client acceptance; do not infer unpublished
Tortoise probabilities from the old implementation.
