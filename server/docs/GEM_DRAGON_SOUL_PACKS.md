# Gem and Dragon Soul packs

Server implementation. Not deployed. Native acceptance remains pending.
Parent [M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.

## Catalog

Installed `itemtype-new.txt` names:

| ID | Name | Leftover reward |
| --- | --- | --- |
| 3005130 | RefinedhGem(B)Pack | one bound gem from 700002, 700012, 700022, 700032, 700042, 700052, 700062, 700072, 700102, 700122 |
| 3005132 | SuperGem(B)Pack | one bound gem from 700003, 700013, 700023, 700033, 700043, 700053, 700063, 700073, 700103, 700123 |
| 3005133 | P4DragonSoulBag | one bound installed item whose purification level is 4 |
| 3005135 | P6DragonSoulBag | one bound installed item whose purification level is 6 |

`AddBoundItem` sets `Bound = true` and ignores its bound argument, so an unbound pack still pays a bound reward. Plus stays 0. The choice is uniform over that installed set. Sorting the purification pools by id does not change that uniform chance. The disposable run loaded 10 refined gems, 10 super gems, 19 purification-level-4 rows, and 27 purification-level-6 rows.

## Ordinary route

Way of Heroes `database/Slayer/StageGoal.txt` is the producer. The historical claim path was `WayofHeroes` `AddBoundItem`; owned substages now use `WayOfHeroesClaims`. There is no shop, exchange, drop table, or monster-owner producer in the bounded scan.

`WayOfHeroesClaims` now owns packet 2830 claim modes. A substage pays only when the saved character meets that stage's `OpenLev` and the server's own records meet `Progress`. Socket stages count defined nonzero gem/socket values in primary equipped slots 1?12, excluding wardrobe and alternate equipment. Duplicate slots require reload. Super-gem stages count defined super gems; empty sockets do not count. `OpenLev` compares rebirth first, then level within that rebirth. The Fierce BOSS catalog says ?Kill?; it requires a `victory` row in `revora_snow_banshee_runs` or `revora_terato_runs` whose `killer` is the character. Terato?s summon party is not kill evidence. The claim packet's number is not progress.

The prize and the `revora_way_of_heroes_claims` row commit with the saved claim blob. A second packet, a concurrent packet, or a reload does not pay again. A full bag or a failed insert records nothing. An older blob that already marks the substage done is stored as a legacy claim and does not create a pack. Login restores a missing blob flag from the claim table and does not grant again. Claim decisions read the durable blob under a row lock. Memory receives the new state only after commit. Historical saves serialize with claims and merge durable flags, so stale memory cannot erase claim history; fenced sessions cannot save. Page requests validate the packet/session and bounded page ID and never add progress.

The third `Prize1` field on these lines is 1. The old substage call ignored it and used plus 0. The old main-stage call passed it as plus. Owned claims keep plus 0 and bound true. Level stages 101, 201, 301, and 401 and rebirth stages 501 and 701 use the same claim transaction. They read the saved level or rebirth. A later rebirth keeps earlier stages open but does not count as having reached a reset level. Subclass stages 504, 608, and 711 use the same claim transaction. Guild stage 303 stays refused after review found split membership/evidence writes; see [SILVER_STUDY_BAGS.md](SILVER_STUDY_BAGS.md). Main-stage claims, super-talisman stage 408, and every substage other than the owned level, rebirth, subclass, socket, and boss stages are refused. Their predicates are not all backed by a server record, so this batch does not invent those prizes or turn the third field into plus.

| Stage | Prize |
| --- | --- |
| 5-2 Famous Socket, one socket | 3005130 x1 |
| 6-1 Hero Socket, four sockets | 3005130 x2 |
| 7-8 Super Hero Socket, eight sockets | 3005132 x1 |
| 8-3 Talent Socket, twelve sockets | 3005132 x1 |
| 7-9 Super Hero Fierce BOSS, Snow Banshee or Terato Dragon once | 3005133 x1 |
| 9-3 Great Talent Embed Super Gem, twelve super gems | 3005135 x1 |
| 10-6 Grandmaster Embed Super Gem, sixteen super gems | 3005135 x1 |

## Use

`GemDragonSoulPacks` owns item-use 1009/action-4 and direct `UseItem`. The four leftover cases throw. One transaction locks the owner, checks the bag, consumes one unit, inserts the bound reward, and writes `revora_gem_dragon_soul_pack_uses`. A stacked remainder stays. A deleted pack reuses its slot, so a full bag of single packs can still open. A stacked pack needs one free slot. An empty pool refuses before any delete. A failed source, reward, or receipt write rolls back and asks the player to reconnect. Reload installs a reward that committed before the live projection failed, and does not pay that open again.

## Disposable validation

`Verify-PotencyHunt.ps1 -GemDragonSoulPacksOnly` covers every installed pool
outcome against its durable receipt, bound and unbound sources, stacked
remainders, full inventory, empty pools, duplicate packets, source/reward/receipt
write failures, reload and post-commit projection failure. REVIEW-322 also
checks locked, suspicious, wardrobe, trading, foreign-owner and malformed-seal
refusals and stale durable binding. These are local non-GM fixtures, not live
player acceptance. The shared census walker recognizes both legacy currency
assignments and the owned `AddConquerPoints` / `AddRevoraPoints` methods.

`Verify-PotencyHunt.ps1 -WayOfHeroesClaimsOnly` keeps the REVIEW-326 socket
and super-gem thresholds, stage gates, packet checks, full bag, concurrent
Save/main/sub claims, reward/receipt/blob rollback, stale legacy repair, and
the synthetic killer-versus-party predicate. It also earns the new prizes
through production actions. Experience from level 1 reaches 20, 50, 80, and
110 and claims 3005117-3005120; each bag pays 5000 silver once. One level
below each threshold, and level 30 against the level-50 open gate, pay
nothing. Production first rebirth claims 3005121 for 10000 silver. Production
second rebirth claims 3005122 for the executable 20000 silver and 400 study,
not the larger catalog purse. A later rebirth left at level 15 does not
receive 3005120. The first-rebirth blade is socketed by the production
SocketerMan packet, equipped, reloaded, claimed as one bound plus-0 3005130,
and opened once to bound plus-0 700002. A production Terato lethal attack
records the attacker as killer. That killer claims one bound plus-0 3005133
and opens it once. The summoner, who is the run owner but not the killer,
receives nothing. Full bag, legacy blob, concurrent packets, and failed item,
claim, or blob writes on the level-20 pack do not pay twice. Equipment stage
102, study stage 303, Chi stage 605, and main prize 1 stay refused. Main-stage
prizes, Chi packs 3005126-3005129, study packs 3005123-3005125, and every
other substage remain unfinished. No deployment or native acceptance is implied.

REVIEW-330 strengthens the earned-path checks: each of the six fortune packs,
the socket pack, and the Terato pack is reloaded and its claim replayed before
opening through packet dispatch. The first-rebirth silver payout also survives
reload and replay with one use receipt. Terato must create the exact selected
phase-4 item, with matching result UID/type in one durable consumer receipt;
unrelated bound boss loot cannot satisfy the assertion. Forged in-memory level
and rebirth values do not replace saved eligibility. These are disposable
production-operation fixtures with supplied prerequisites/materials and a
supplied lethal damage value, not a complete player journey or live acceptance.

## Remaining luxury pack

LuxuryGoldEquipmentPack 3005368 stays a leftover split-write. `Chat.cs` command `testitem` builds that id in memory and sends it; that is not a shop, drop, or StageGoal grant. Its `Random.Next(1, 4)` result is 1, 2, or 3, so the branch that would add 7 Conquer Points is unreachable. The catalog text about a blessed Gold Cloth and King of Scorpions is not what the case does. No ordinary acquisition route was found, so this batch does not invent one.
