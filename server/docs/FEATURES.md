# Live features on Revora 6711 3D

These are ordinary-player features on the working 6711 DX9 server. None of them require GM status unless marked.

## Character status correction (deployed; client verification pending)

Initial character information now uses the confirmed 6711 HP/mana field layout
and a stable character UID for the Domino display code. This applies to every
character at login. Existing saved attributes, currency balances and prestige
formulas are preserved. The build passed 29 packet checks and both server
handshake probes after restart; live status-window verification remains pending.

## Wall of Prestige (deployed; client verification pending)

The wall's personal score, overall/class rankings, category breakdown and winners
now use the same prestige calculation. CPs and Domino currency no longer drive
the leaderboard. Rankings include saved offline characters, with deterministic
ties, complete ten-row pages and a top-100 limit. The wall receives the overall
winner, winners for populated classes (including Windwalker), and the central
winner's appearance. Empty classes have no invented entries. Equipment inspection
uses the existing online inspection path; offline targets receive the client's
unavailable-player response. All ordinary authenticated players can use the wall;
synthetic adventurers are excluded and requests are rate limited.

The September 13 build passed 55 prestige plus 29 CharacterInfo checks, a read-only
saved-data reload check for both existing characters, and startup handshake probes.
Genuine-client rendering and an authenticated save/relogin test remain pending.

## City guards

Player-shaped officers (not chicken meshes) stand in the cities. They hunt nearby city pests inside an 18-tile post and still prioritize PK players. They do not hire, carry, or loot. Killing a city monster as a guard does not mint player loot.

Commander talker NPC **91001** remains in Twin City.

## Living-world adventurers

Labeled `[Adv] …` wanderers spawn in Twin City, Desert City, Phoenix Castle, Bird Island, Ape Mountain, and Market. They walk, sit, greet, wear a Super Tempest Wing +12, and show Chi plus Jiang Hu on the spawn packet. They fight nearby city monsters; monsters fight back. They are not overpowered and they do not steal player drops.

## Whisper hire and follow (no AI model)

Right-click an adventurer and whisper:

| Whisper | Result |
|---|---|
| `hire` / `follow` / `come` / `join` | They follow you and fight pests near you |
| `wait` / `stay` / `stop` | They hold the tile |
| `dismiss` / `bye` / `leave` | They return to their home city |
| `help` | They list those commands |

One companion per player. Logout dismisses them. They do not pick up loot or Conquer Points. City guards refuse hire: they keep the watch.

## Auto-hunt gold fix (deployed; client verification pending)

Eligible VIP 1+ characters automatically collect accessible ground gold within
three tiles while auto-hunt is active, up to eight piles per second. Ownership
protection and team money-sharing settings apply. Dead or trading characters
cannot collect, and gold behind walls remains on the ground. Manual pickup still
requires standing on the pile. CPs and equipment are unaffected. The fix passed
25 isolated pickup checks and was activated on September 13. Both listeners
passed handshake checks; genuine-client validation is still pending.

## Companion AI update (deployed; client verification pending)

The September 13 AI update is installed and enabled for eligible ordinary players.
It passed 57 isolated assembly checks and startup listener checks, but has not
been tested in a live 6711 client session. The updated orders below supersede
the earlier hire/follow description above.

| Whisper | Updated behavior |
|---|---|
| `hire` / `follow` | Follow at two-tile spacing, assist your attacks, and defend against attacking monsters; reset prior orders |
| `assist` | Prefer your recently attacked monster, then threats to either of you |
| `protect me` / `protect` | Fight monsters attacking either of you; prioritize threats to you |
| `passive` | Stop attacking and casting; retain follow or hold movement order |
| `hold here` / `hold` / `wait` | Save the current tile; attack only from there; return if displaced |
| `stay back` | Follow at four-tile spacing while retaining combat stance |
| `retreat` | Cancel hold, return toward you, and remain passive until another combat order |
| `dismiss` | Release ownership and return home |

Companions route around terrain and avoid diagonal corner cutting. At distances
of five tiles or more they try a clear jump of up to six tiles, otherwise use
up to two routed walking steps. Distant following allows recovery after eight seconds away when
neither character has a nearby pursuing monster. Map changes use valid landing
tiles; hold stays on its original map. Dynamic maps, spectators, qualifiers and
the checked event families release the companion rather than bypass entry rules.

Warriors draw an engaged monster's attention when they hit. Archers keep ranged
spacing; other weapon classes use calculated melee hits. Water Tao companions
prioritize Cure below 70% health, then Thunder; Fire Tao companions use Tornado.
These three spells use existing server calculations, table mana costs, sight,
range and action cooldowns. Other learned skills are not randomly displayed as
if they had executed. No Windwalker implementation is included.

Low-health companions stop attacking below 25% HP and resume at 60%. Resources
recover on a two-second cadence (40 mana, 10 stamina); health recovers by 1%
after ten seconds without an attack and with no nearby pursuer. Holding and
passive orders remain authoritative. Companions ignore bosses, players, pets,
guards and monsters currently engaging another player. Existing no-drop behavior
for companion finishing blows is retained: owner kill/quest credit is not added.

One companion per owner is enforced atomically. Commands and decisions serialize
per companion; overlapping world ticks are skipped. Each region has at most four
adventurers including hired ones (24 total); each path search expands at most
256 nodes and retries failed searches no faster than once per second. Monster
candidate scans still use the existing map entity collections. Orders and hire
state remain session-only, and logout releases ownership.

See `tests/CompanionAI/README.md` for repeatable validation and remaining client
checks. This is the first AI pass, not full class-skill or official AI parity.

## Courier Loop (NPC 900000017)

Rewards now commit with completion in one transaction; see [earning and persistence evidence](COURIER_LOOP_TRANSACTIONS.md). Native/live acceptance is pending.

Twin City **1002 (319, 303)**, four tiles north of Rowan. Talker **Cerys**. Ordinary players. Once per reborn.

1. Accept from Cerys.
2. Report the cut satchel to the **City Commander** (442,339).
3. Hunt **20 Bandits** in Phoenix Castle (1011). Killer-only credit. She can send you.
4. Show **PurseClerk** (418,330).
5. Return to Cerys.

Turn-in: **8,000 CPs**, **80,000 gold**, **200 Revora Points**, a random class-locked bound P7 soul, a puff to **120** if below, and **seven days of VIP 6**. Lifetime VIP 6 and VIP above 6 are left alone. Prizes in `System/Config.cs`.

## Pathfinding window

Client `ini/npc.ini` `Note=` tags fill the Pathfinding list. Revora desks use unique meshes 46000–46060 (`NpcType4600`–`4606`) so they are not all Taoist Moon. Launcher patch **1.0.2** ships those notes plus Questinfo. Restart Conquer after the launcher update.

## Quest List GUI

The 6711 Quest List window is packets 1134/1135. **Every new player quest must call** `QuestListSystem` (`MarkAccepted`, `MarkProgress`, `MarkCompleted`, `MarkAvailable`) and add a `MissionId` block to the client `ini/Questinfo.ini` (name, target, reward). Server IDs: Path Hunt **90001**, Courier Loop **90002**, Soul Trial **90003**, Daily Envoy **90004**. Do not dump leftover TQ missions into the list.

## Path Hunt (NPC 900000016)

Twin City **1002 (319, 299)**, four tiles north of Caelan. Talker **Rowan**. Ordinary players. Once per reborn.

Hunt **20 Pheasants**, **20 Robins**, **20 Turtledoves**, and **20 Apparitions** in Twin City. He can send you to each flock. Kill credit is on the killer only.

Turn-in: 3 Exp Potions (`723017`), 25,000 gold, 100 Revora Points, and a level puff (first life to **80**, first reborn to **95**, second reborn to **105**). If you are already at the puff, extra Exp Potions instead. Cerys (319, 303) pays the courier purse. Caelan (319, 295) has the Soul Trial. Prizes in `System/Config.cs`.

## Soul Trial (NPC 900000013)

Twin City **1002 (319, 295)**, west of Last Man Standing. Talker **Caelan**. Ordinary players. Once per reborn.

Hunt **50 Birdmen** on Bird Island (1015), **50 Macaques** on Ape Mountain (1020), and **50 Sand Monsters** in the Desert (1000). He can send you to each map. Kill credit is on the killer only.

Turn-in: a **random class-locked bound P7 soul stone**, 5 Exp Potions (`723017`), 50,000 gold, 500 Revora Points, and a level puff (first life to **90**, first reborn to **100**, second reborn to **110**). If you are already at the puff, extra Exp Potions instead. Prizes in `System/Config.cs`.

## Steed Race (NPC 900000012)

Twin City **1002 (370, 311)**. Twice a day at **13:15** and **21:15**. Equip a steed and Riding. Top three claim at the clerk. Prizes in `System/Config.cs`. Old 100,000 Revora Point purse is gone.

## Duel Pit (NPC 900000011)

Twin City **1002 (370, 307)**. Twice a day at **11:15** and **19:15**. 1v1. Winner stays, loser goes to Twin City. Sixteen rounds. First through fourth claim at the clerk. Prizes in `System/Config.cs`.

## Boss Rush (NPC 900000010)

Twin City **1002 (370, 303)**, four tiles north of BeastTrial. Every hour at :45. Solo only. Private map. Fight five bosses one at a time (Misery Monk through Tyrant Nemesis). Heals stay on. Two lives. Twelve minutes. Win claim includes a Radiant Star Stone (Perfection). Prizes in `System/Config.cs`.

## Vesper (NPC 900000014)

Twin City **1002 (396, 340)**, Prizewell's old tile. Revora Point luxury stall. P7 souls, Perfection stones, 4-star Legend garments, 5-star looks, star mounts, relics, and the **Khopesh** blade. Prices named before payment. Ordinary players.

Khopesh is a **Blade (410)**, not a Sword (420): single-edged sickle-sword. Super **410449**, level 110, same attack ladder as MythicBlade. `KhopeshRevoraPoints` (**5,000 RP**). Looks use the hooked 430 mesh so it reads as a crescent blade in the hand. Inventory icons `410443`–`410449`.

## Beast Trial (NPC 900000009)

Twin City **1002 (370, 299)**, four tiles north of WaveWarden. Odd hours at :30. Solo only. Private map. Kill 8 waves (mini-boss 4, named boss 8). Heals stay on. Three lives. One attempt per round. Prizes in `System/Config.cs`.

## Wave Defense (NPC 900000007)

Twin City **1002 (370, 295)**. Even hours at :30. Teams of 2-4 get a private map. Empty slots become `[Adv]` allies. Hold three posts through 8 waves (mini-boss 4, named boss 8). Player heals are off; the Warden Medic pulses every 30 seconds. Four lives for the whole team. Prizes in `System/Config.cs`.

## RelicRush (NPC 900000005)

Twin City **1002 (366, 295)**, next to NobleWar. Odd hours (1, 3, 5, …, 23, skipping 15:00 NobleWar). Register 5 minutes before. Max 24. Hold the relic to score; death drops it and sends you out. Top three claim at the clerk. Prizes are in `System/Config.cs`.

## RevoraMaster (NPC 900000001)

Twin City **1002 (410, 354)**. Prices are named before payment. Edit `System/Config.cs` (`RevoraMasterPeakPrice`, `RevoraMasterVestmentsPrice`, `RevoraMasterMountKitPrice`, `RevoraMasterRenamePrice`, `RevoraMasterVipPrice`, `RevoraMasterVipLevel`).

| Service | Config field | Notes |
|---|---|---|
| Peak training | `RevoraMasterPeakPrice` | Level 140, class arts, composed weapons |
| Class vestments | `RevoraMasterVestmentsPrice` | Bound Super set into inventory, not auto-equipped |
| Name change | `RevoraMasterRenamePrice` | Reconnect in Twin City under the new name |
| Mount kit | `RevoraMasterMountKitPrice` | Steed, Heaven Fan, Star Tower, Riding Crop, Tempest Wing |
| Lifetime VIP | `RevoraMasterVipPrice` / `RevoraMasterVipLevel` | Does not expire |

Mount kit rules: Fan Super Thunder, Tower Super Glory, Wing Thunder + Glory, Crop no sockets, all +12 −7, bound. The kit is legal for every player (not a staff exemption). Real junk (Enchant &gt; 0, Crop sockets, Bless &gt; 7, wrong gems) still jails.

## Sereth the Returned (NPC 59558)

Twin City **1002 (396, 364)**. Combined first and second rebirth. Second rebirth requires an Exemption Token **723701**. Reborn flags persist immediately so a second life is not lost if a later save fails. Gender change costs `SerethGenderPrice` (25,000 CPs). Genuine 6711 bodies only (1003/1004 ↔ 2001/2002). Reconnect in Twin City. Dr.YinYang **1100** points here.

## Chi and Jiang Hu training prices

The Chi window refill costs **1,000 CPs** from zero to 4,000 Chi
(`ChiFullRefillCp` in `System/Config.cs`), matching the **1 CP = 4 Chi** rate in
the [official Chi guide](https://co.99.com/guide/guides/chi_system.shtml)
(checked 2026-09-13). Partial refills cost proportionally less, rounded up to
a whole CP. At or above 4,000 Chi, refill does nothing and
charges nothing. The same price applies to the existing automatic refill when
studying with fewer than 200 Chi points.

Normal Jiang Hu CP training costs **10, 20, 30, ... up to 500 CPs per attempt**,
using the existing paid-training counter. This follows the
[official incremental pricing guide](https://co.99.com/news/2013-07-25/age_of_jiang_hu_tip_ii_how_you_assign_suitable_attributes.shtml).
Restoring a previous active star costs **20 CPs**, as shown in the
[official guide screenshot](https://hw.99.com/uploads/co/images/guides/quests/2013/jianghu/v1/019.jpg).
Free training uses **10 Study Points**, one Talent and one Free Course, as
specified by the [official Jiang Hu guide](https://co.99.com/guide/quest/2013/the_age_of_jiang_hu.shtml).
These prices are configured by `JiangHuTrainingStepCp`, `JiangHuTrainingMaxCp`,
`JiangHuRestoreCp` and `JiangHuFreeTrainingStudyPoints`. Sources checked 2026-09-13.
This change covers the existing normal/free training and CP restore paths;
premium quality options and pill/bound-CP payments have not been implemented
or verified by this change.

## ChiJiang Clerk (NPC 900043)

Twin City **1002 (396, 348)**, between Vesper and the P7 desk. Paid Chi / Jiang Hu hunt. Ordinary players, not GM-only.

Walk-up gate: level **110** and first rebirth (`ChiJiangMinLevel` / `ChiJiangMinReborn`). Hunt entry is `ChiJiangHuntCp` (**5,000 CPs**), not the leftover 1,000,000 CP lock. Chi Beast is map **20001 (41, 71)**. Jiang Hu Beast is map **20000 (41, 71)**. Exit clerks **900044** / **900045** send Twin City **1002 (327, 279)** for free.

Packs drop from the beasts. Exchange at the clerk: about `ChiJiangRarePercent` (**25%**) is a Power Chi Pack (**3001607**) from a Chi Pack (**3000060**), or a Brave Medal (**722314**) from a Jiang Hu Pack (**3000061**). The rest is a Magic Spar (**3006594**, 200,000 gold). Inventory space is checked. This is a paid desk, not a Quest List mission.

## Twin City plaza

Leftover private-server event clerks (Extreme PK, Weekly PK, Elite PK, CTF, CP rain, anniversary boxes, old SuperBoss desk, duplicate SteedRaceMan, VIP CP maps, and similar) are jailed to map **6000**. Their handlers stay closed. Shop flags, warehouses, and genuine city services stay.

| District | Who | Tile |
|---|---|---|
| West hunts | Caelan, Rowan, Cerys, Daily Envoy, Clan Manager, Guild Director | 319,295–325 |
| Event row | LMS, Expedition, NobleWar, RelicRush, WaveWarden | 354–370, 295 then north to SteedRace 370,311 |
| Cultivation | Vesper, ChiJiang Clerk, P7 Sacred Treasure, Sereth | 396, 340 / 348 / 356 / 364 |
| East services | RevoraMaster, PurseClerk, Conductress, Commander | 410,354 / 418,330 / 423,356 / 442,339 |
| Service row | Merchant, Magic Artisan, Name Registrar, Dr.YinYang, Barber, Luna, MillionaireLee | 304–305, 408–433 |
| Combat | Area Guard (arena), Boxer (training camp), Captain Li (jail visit), Mount Trainer | 435,282 / 428,282 / 535,335 / 430,403 |

SolarSaint **3215** stays at **327,250** as a closed Perfection notice. He does not teleport into the parked trial.

## Twin City service row (304–305)

| NPC | Name | Service | Price |
|---|---|---|---|
| 50404 | MerchantClerk | Merchant account (1-day wait) | `MerchantApplyPrice` 25,000 CPs |
| 10062 | MagicArtisan | Low-level equipment window | Free |
| 803 | NameRegistrar | Client name window | `RevoraMasterRenamePrice` 2,500 CPs |
| 1100 | Dr.YinYang | Points to Sereth | — |
| 10002 | Barber | Native hairstyle collection; permanent ownership and colour selection | Installed silver prices; VIP 4 styles require eligibility at purchase |
| 4178 | Luna | Moon Platform 1103 | `LunaTeleportGold` 300 gold |
| 11275 | MillionaireLee | Pack stones / gems / balls | Free (items in, packs out) |

## Prizewell (NPC 900035)

Removed from Twin City. Vesper holds the Revora Point stall. Prizewell is jailed so the database row cannot reload on the plaza.

## Other live rules

- Unsolicited event auto-invite popups are off for everyone.
- Facebook / ExtremeTop vote prompts were removed.
- Player-facing NPC dialogue is English.
- Heartbeat saves the character about every 60 seconds.
- Unknown or broken login maps fall back to Twin City square **1002 (300, 278)**.
- Twin City gate scroll **1060020** still sends **1002 (300, 278)**.


### Twin City progression shops (patch 1.0.8)

Native CP shops: SubclassSupplies at 424,339 (11 items) and InnerPowerShop at 428,343 (all 17 books). Vesper at 396,340 now offers 33 level-1 runes through its Runes dialogue category for 500 yellow / 750 blue / 1,000 red RVPs. Study books consume one item and persist the correct award. Full prices, persistence behavior and validation are documented in [PROGRESSION_SHOPS.md](PROGRESSION_SHOPS.md).

## Potency Hunt

- PotencyGuide: Twin City (432,347), free entry; field guide (559,547), free return.
- Map 1217: 1,000 additional PotencySerpents, slightly stronger than AlienSerpent,
  replenished about 1�1.5 seconds after death.
- One loot roll per kill: 10% Outstanding (400), 5% Excellent (600),
  1% Supreme (1,000), 84% no pack. Pick up and open packs for Potency.
- Ordinary players can enter, collect and use packs. Atomic award/consumption
  with checkpoint recovery; see POTENCY_HUNT.md for details and limits.

Pheasant flocks in Twin City now spawn twice their database baseline: 200 + 80 + 200 = 480, with existing locations and respawn timing.

Daily world bosses: four local-time windows (03/09/15/21), independent persisted minutes, separate hunting maps, two-hour expiry, 100M HP, killer bounty 1M CP/50K RVP/10M gold, four random P7 souls. See [DAILY_WORLD_BOSSES.md](DAILY_WORLD_BOSSES.md). Boss HP spawn/update values now carry actual HP; client 1.0.12 adds missing daily and Boss Rush profiles.

Extraction now uses a large Bird Island instance with separated entry, guarded caches, three staged exits and an optional minute-ten Specter. Claims persist atomically. Relic Rush adds a double-point final minute and carrier coordinates; Wave Defense adds regroup breaks, recovery and surviving-post targeting. See [EVENT_IMPROVEMENTS.md](EVENT_IMPROVEMENTS.md). Proposed future daily adventures: [DAILY_QUEST_IDEAS.md](DAILY_QUEST_IDEAS.md).
