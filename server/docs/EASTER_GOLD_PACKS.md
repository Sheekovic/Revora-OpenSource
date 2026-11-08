# Leftover EasterGoldPack 720121 and classified JoyCPPack 720654

M6.56b9l, 2026-09-23. Server implementation for 720121; 720654 classified.
Not deployed.

## 720121 EasterGoldPack / PaqueteSanto

Ordinary 1009/action-4 and direct `UseItem` go through `EasterGoldPacks`. The
legacy case fails closed. Leftover used `Random.Next(1,31)` (reachable 1–30)
and `new Random()` after `Inventory.Remove`. Case 31 (`193785`) is
unreachable. There is no leftover success text. Outputs are leftover
`Add(id,0,1)` unbound plus-0 catalog-durability garments and mounts.

Owned service uses an injectable `Roll(1,31)`, never inserts catalog-missing
item 0, consumes one unit, and keeps stack remainder. A retained source stack
refuses with `Constants.FullInventory` when the reward would not fit; a
deleted source slot is credited in a full bag.

Receipt table `revora_easter_gold_pack_uses`. Direct daily P7 is unchanged.

### Ordinary issuance

| Route | Disposition |
| --- | --- |
| Daily Sign-In `SignInTable.Load` + `MsgSignIn` | **Live.** `signin.ini` 7-day prize is 3100014 and 14-day is 3100015. Owned Daily Sign-In use of those packs still issues 720121. Packet path needs level 140, second rebirth, VIP ≥5 and 4,000 Revora Points (late sign VIP 7). |
| 20× HolyStone 3004246 combine | **Live, owned.** `AncientStoneCombine` consumes 20 units at `Count<=31` and issues 720121. SuperBoss VIP7 floor loot of spawned FuriousBear can drop 3004246. |
| Prizewell 900035 Holy Package (100,000 RVP) | **Closed.** Twin City `HandleClosed` / `ClosedDeskIds` includes 900035. |
| SlayerPointshop.ini `720121 300000` | **Unreachable.** Dedicated UID 6001 `REVORAPointhop.Load` is commented out. |
| `Data.FirstCredit` bound Holy Pack | **GM.** `primercredito` chat points. |
| SunDevil `MonsterTable.Drop` | Coded drop, but the reviewed Golden War admission NPC 900018 is closed by `TwinCityPlaza`; ordinary access is unproven. |
| CursedChief table case 9 | Unreachable: `Random.Next(1,9)` never yields 9. |

## 720654 JoyCPPack (classified, not owned)

Catalog copy is JoyCPPack / 1,380 CPs. Leftover `UseItem` pays **+25,000
Revora Points** and remains live-split-write. Visible issuance is bound
`Data.FirstCredit` gated by GM `FirstCreditPoints`. `DropItemTable` lists
720654 only in a commented row. No shop, exchange or StageGoal producer.
Do not move it onto an owned RVP service without an ordinary producer.

## Remaining work

Catalog-only Chi pills 3000051/729476–729479, catalog-only Surprise box 722178,
nested/gear leftover 721222–721245, classified silver leftovers, leftover
medicine bundles 720010–720017, classified 720654, other leftover packs, Host
restart, native acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.


### Supplier access correction (2026-09-30)

A read-only query of the isolated candidate confirmed SunDevil family 7156 on
map 1037 at 227,231 (one configured spawn), and FuriousBear family 3194 on map
1364 at 457,447 (15 configured spawns). This is spawn evidence, not admission.
The SunDevil map's legacy Golden War NPC 900018 is explicitly closed; testing a
forced teleport would not establish ordinary access. FuriousBear admission to
1364 also remains unverified. Existing source/catalog tests must not be read as
an end-to-end earned Easter supplier certificate.

The sign-in path needs its missing runtime catalog and transactional issuance
repair; see the [current sign-in follow-up](DAILY_SIGN_IN_PACKS.md). All 30 pack
outputs remain a potential supplier pool only until one eligible ordinary player
can acquire, open, deposit and recover them from an accessible source.


The [sign-in earning fixture](SIGN_IN_ISSUANCE.md) now proves one actual Easter
pack and permanent mount from a boss-funded seven-day sign-in milestone, with
cold recovery. Its controlled output is mount 200481. The complete Easter pool
and combined 50-mount collection remain unproven; blocked Golden War admission
is not bypassed or reopened by this change.
