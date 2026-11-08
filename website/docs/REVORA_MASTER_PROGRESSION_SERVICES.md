# RevoraMaster progression services

RevoraMaster is a first-party, declarative patch-5165 service NPC. It is placed at
map 1002, cell (349,359), with stable legacy UID `900000001`. The approved
`newplain.DMap` marks that exact cell accessible. The NPC uses the existing bounded
2031/2032 dialog state machine; it does not execute donor scripts, dynamic code, or
SQL supplied by a client.

## Evidence and limits

- The exact client status ordinals and 1006 offsets are recorded in
  [PROTOCOL_RESEARCH.md](PROTOCOL_RESEARCH.md).
- The authorized owner source and `ExpNeed.dat` both end normal progression at
  level 130. At the cap, stored experience is zero and the ten levels beyond the
  level-120 base-stat catalog contribute 30 unallocated attribute points.
- First rebirth is available at level 120, except the reviewed Water Taoist rule at
  level 110. First and second rebirth reset to level 15 and record immutable class
  lineage. Only Trojan, Warrior, Archer, Water Taoist, and Fire Taoist targets are
  enabled because those are the rebirth targets corroborated by the donor and the
  active item/stat catalogs.
- Silver and Conquer Points are capped at `2,000,000,000`, the signed-display-safe
  value supported by PostgreSQL constraints, server types, 10017, 1006, and the
  exact client. `@givemeall` sets rather than adds, so it is idempotent.
- Skills and unrelated proficiencies are preserved. A class-loadout purchase sets
  only the weapon proficiencies represented by its granted weapons to the evidenced
  patch-5165 maximum of level 20. The reviewed service does not inherit the donor's
  implicit destructive resets. Incompatible equipped items are moved
  atomically to reserved inventory slots or the entire rebirth is rejected.

## Paid services

Each confirmed service costs exactly 1,000,000 CPs. Validation, CP deduction,
mutation, receipt, and sanitized audit are one database transaction. Cancelled,
invalid, unaffordable, duplicate, or incomplete requests do not charge. Production
starts with a canary rollout bound to SHEEKOVIC's authenticated AccountId and
immutable Character UID; global rollout requires a separate operation.

| Service | Persistent result |
|---|---|
| Maximum level | Level 130, canonical base stats, zero experience, derived HP/MP |
| First Rebirth | Count 1, original/first class lineage, chosen class, level 15 |
| Second Rebirth | Count 2, complete lineage, chosen class, level 15 |
| Class loadout | One new bound set, highest selected-class rank, and level-20 granted-weapon proficiencies |

## Versioned class loadouts

All entries are active-catalog item definitions, Super-quality IDs, Plus 12, Bless
7, Enchant 255, full durability, color 3, and two reviewed patch-5165 sockets.
Physical sets use Super Dragon gems for offensive slots and Super Tortoise gems for
defensive slots; Taoist offensive slots use Super Phoenix gems.

| Class | Head | Necklace | Armor | Ring/bracelet | Boots | Main hand | Off hand |
|---|---:|---:|---:|---:|---:|---:|---:|
| Trojan | 118109 | 120249 | 130109 | 150249 | 160249 | 410339 | 420339 |
| Warrior | 111109 | 120249 | 131109 | 150249 | 160249 | 480339 | 900109 |
| Archer | 113109 | 120249 | 133109 | 150249 | 160249 | 500329 | — |
| Water Taoist | 114109 | 121249 | 134109 | 152259 | 160249 | 421339 | — |
| Fire Taoist | 114109 | 121249 | 134109 | 152259 | 160249 | 421339 | — |

The service promotes the selected family to its highest normal rank: Trojan Master
`15`, Warrior Master `25`, Archer Master `45`, Ninja Master `55`, Water Saint `135`,
or Fire Saint `145`. It masters only the weapons in that set: blade `410` and sword
`420`, club `480`, bow `500`, katana `601`, or backsword `421`; the Warrior shield is
not treated as a weapon proficiency. Items, promotion, proficiency changes, charge,
receipt, and audit commit atomically. A later deliberate purchase is a new confirmed
transaction; replaying the same confirmation cannot charge or create another set.

The maximum-level service applies the same highest-rank and class-weapon mastery
mapping. If a character is already level 130 but is missing either state, choosing
the service reconciles both without charging CPs or granting level/stat benefits a
second time. A fully reconciled level-130 character remains an unchanged no-charge
`already-maximum-level` result.

## GM command

`@givemeall` and `@clearinv` are the reviewed GM mutation commands. Authorization is re-evaluated in
PostgreSQL from authenticated AccountId, immutable CharacterId, role `gm`, and an
unrevoked assignment. Character names, packet UIDs, IP addresses, and client flags
are not authorization inputs. Non-GMs receive `That command is not available.`
without state change or disconnection. Both commands are bounded per session.
`@clearinv` atomically moves only carried inventory items to the server-only
`gm-discarded` state, preserving immutable item and economy history. Equipped,
warehoused, traded, and personal-shop items are untouched. The server sends one
verified inventory-remove action for each affected item and records an append-only
receipt; repeating an empty clear is a safe no-op.

## Rollback boundary

The Host is independently replaceable with its timestamped pre-deployment copy.
Migration V033 is additive. If a database rollback is required, stop the Host and
restore the protected pre-V033 PostgreSQL archive; never reverse only wallet,
progression, receipt, and generated-item rows by hand. The separate protected
SHEEKOVIC snapshot is the narrowly scoped option for owner acceptance state only.
