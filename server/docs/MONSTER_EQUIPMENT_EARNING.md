# Monster equipment and composing contribution

Baseline `4d304a6`. M6.37 restores an ordinary monster +1 equipment source and
atomic pickup/contribution delivery. Native 6711 acceptance and deployment remain
separate pending work.

## Why a loot source was necessary

The compiled MonsterInformation.Drop routine contains no assignment that generates
+1 equipment. Its generic OwnItemID branch can theoretically emit equipment, but
the read-only live catalog inspection found 615 monster definitions, zero equipment
ownitem entries, and 591 zero ownitem entries. Adding a pickup hook alone would
therefore leave composing contribution unavailable through ordinary monster loot.
The ignored read-only inspector SHA-256 is
`1F08BC4839DDD60A3CBE6C5B4A290F6475AA69FD35A36B25E07706E86C0BB062`.

The [official mentor guide](https://co.99.com/guide/guides/mentors.shtml) establishes
one contribution point for a monster-dropped +1 item and 100 points per +1 Stone.
It excludes purchases and trades. Its text does not establish an equipment drop
rate. The new chance is an explicit Revora rule.

## Restored route

Normal monsters on Twin City 1002, Phoenix Castle 1011, Ape City 1020, Desert City
1000 and Bird Island 1015 receive one roll per death. Bosses, guards, revivers,
companions, living-world adventurers, live/unregistered monsters and dynamic maps
are excluded. Existing specialized event drop handlers keep their earlier exits.
The default chance is 100 basis points (1%) in
`Config.MonsterPlusEquipmentBasisPoints`, bounded to 0?10,000; zero disables it.
The existing OwnItemID drop and gold rates are unaffected.

The item comes from installed normal-quality common necklaces, bags, rings,
bracelets, boots and classic weapon families. It requires no higher level than
the monster and lies within nine levels of the highest available common tier,
capped at item level 120. It is unbound, Plus=1, stack=1, with its existing catalog
durability. Neither elite/super gear, epic equipment nor consumable +Stones are
invented as monster equipment. Floor IDs skip occupied NPC and item identities.

The killer retains the existing 15-second ownership window; eligible teammates
can collect according to PickupItems. Thereafter another eligible collector can
pick it up. The 20-second ground lifetime remains unchanged. The collector's
current mentor receives the composing point, independent of who killed the mob.
Players without a mentor still receive the equipment.

## Ownership and persistence

Private server metadata ties the exact generated floor object to its monster,
item identity and receipt key. MobDropped alone cannot establish that origin.
Purchasing, trading, administrator-generated objects or dropping an existing item
cannot mint another point. Native 1101 requests supply only a target identity;
the server validates their frame, current map object, actual coordinates, loot
rights, lifetime, inventory capacity and authoritative membership.

Pickup commits the exact item row, one composing point and its receipt in one
transaction. The two contribution counters preserve wide durable values while
capping only native ushort displays. Inventory and both relationship views update
after commit; an offline mentor can reload the point later. Claiming the resulting
100 points uses the existing M6.35 stone claim transaction.

Collector ownership, a shared floor-object lock, contribution ownership and database
row locks serialize competing requests. Expiry removes the same managed floor
object under that lock and cannot delete a replacement with a recycled UID.
The row and receipt identities prevent a successful pickup from granting again.
Failed inventory, contribution or receipt writes leave the unclaimed floor item
available until its original expiry and fence the collector for reload. Inventory
capacity and stale/ambiguous state cannot silently consume loot or contribution.
Ground items remain ephemeral; a server restart does not recover unclaimed drops.

## Validation

The targeted suite exercises actual generation and native pickup, includes the
production monster Drop dispatcher, and earns one claimed +1 Stone from 100
separate generated-and-picked equipment items. It also checks duplicate deaths,
chance disabling, source exclusions, competing collectors, loot ownership expiry,
full inventory, removed/expired floor objects, malformed packets, exact item reload,
offline mentors, immutable origin, stale inventory and historical duplicate rows.
Fault injection covers contribution, item and receipt writes, including silently
skipped inserts. All 2,635 related checks across 37 suites passed before the final
floor-ID collision correction. The final build then passed 47 equipment checks,
213 base/boss checks, 25 gold pickup checks, 33 guard checks and 16 source/overlay
audits: **334 final checks**. The collision test confirms both an existing NPC
and an existing floor item retain their identities when a new drop is created.

Final isolated `Revora.exe` SHA-256:
`7C9C79760BE3AD89D28147274EB4BBB96C2CA549CEC2406F2D77EB8EB3C5A007`.
Logs are in ignored `obj/chi-refill-validation/monster-equipment-*.log`.
Fixture database writes remain confined to disposable validation schemas.

No public database, live server binary, client content or public patch is changed.
The broader mentor BP/native lifecycle and historical duplicate remediation gates
remain open. This restores the named wilderness earning route, not all legacy loot
systems or unverified original drop probabilities. Achievement producer coverage
remains 101/337; this prerequisite does not fabricate an achievement receipt.
