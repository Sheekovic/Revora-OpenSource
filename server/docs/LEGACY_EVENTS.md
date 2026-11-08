# Legacy events and event NPCs (parked)

This is the historical parking inventory. The Kept section records earlier active
services; newer staged restorations are tracked in [the quest/event roadmap](QUEST_EVENT_ROADMAP.md).
Local CTF, Elite PK and Power Arena now have separately validated implementations,
with live activation still pending. Their old timers and reward paths remain retired.
Anwen, RevoraMaster, Commander, city guards, and living-world wanderers are services.

Code still exists in `Thread.cs`, `Npcs.cs`, and `Game/Features/` so it can be copied or rewritten. Do not re-enable these timers without a new design.

## Kept

| Name | Where | Notes |
| --- | --- | --- |
| Last Man Standing | LastManStanding, Twin City 1002 354,295 (npc 900000003) | 12 times a day, even hours. Register 5 minutes before. Max 32. Death is out. Winner claims 20,000 CPs, 2,000,000 gold, 50 Revora Points at LastManStanding. Arena is a dynamic copy of map 700. |
| NobleWar | NobleWar, Twin City (NPC 10620's old tile, npc 900000004) | Once daily 15:00. King / Prince / Duke / Earl brackets on dynamic copies of map 700. |
| Expedition | ExpeditionClerk, Twin City 1002 358,295 (npc 91002) | Every 3 hours. 5 minute register, 16 seats, 20 minutes. Solo. Extract after minute 5. |
| RelicRush | RelicRush, Twin City 1002 366,295 (npc 900000005) | Odd hours (skips 15:00). Hold the relic to score. Death drops it. Top three claim CPs, gold, and Revora Points. |
| Wave Defense | WaveWarden, Twin City 1002 370,295 (npc 900000007) | Even hours :30. Teams of 2-4 on a private map. Hold 3 posts through 8 waves. Medic-only heals. 4 team lives. |
| Beast Trial | BeastTrial, Twin City 1002 370,299 (npc 900000009) | Odd hours :30. Solo PVE. Private map. Kill 8 waves. Heals on. 3 lives. |
| Boss Rush | BossRush, Twin City 1002 370,303 (npc 900000010) | Every hour :45. Solo PVE. Five world bosses one at a time. Heals on. 2 lives. 12 minutes. |
| Duel Pit | DuelPit, Twin City 1002 370,307 (npc 900000011) | Twice daily 11:15 and 19:15. 1v1. Winner stays 16 rounds. Top four claim. |
| Steed Race | SteedRace, Twin City 1002 370,311 (npc 900000012) | Twice daily 13:15 and 21:15. Equip a steed. Top three claim. |
| Soul Trial | Caelan, Twin City 1002 319,295 (npc 900000013) | Once per reborn leveling hunt. Class-locked P7 soul. Not an event. |
| Vesper | Twin City 1002 396,340 (npc 900000014) | Revora Point luxury shop. Prizewell's old tile. Not an event. |
| Anwen | Birth Isle 1007 25,28 | Welcome kit, not an event. |
| RevoraMaster | Twin City 1002 410,354 | Paid VIP / kits, not an event. |
| City Commander | Twin City 1002 442,339 (npc 91001) | City Guard desk. Fully equipped officer look. Stays at post. |

## Hourly (disabled)

These fired every hour from `Thread.CharactersCallback`. Popups used `GameState.MessageBox` (no-op while AutoInvite is off) plus center-screen text.

| Minute | Name | Teleport / map | Notes |
| --- | --- | --- | --- |
| :00 | Golden Octopus (GoldOctopus) | Twin City 309,248 then Deep Sea 2 210,154 | `Kernel.SacarPulpo`. NPC spawn when flag is false. |
| :06 | Ice King (IceKing) | Twin City 309,248 / Kingdom 304,309 | `Kernel.SacarBruja` |
| :12 | Chaos Ripper (ChaosRipper) | Twin City 309,248 / Frozen Grotto 5 349,174 | `Kernel.SacarGuardian` |
| :18 | NemesisTyrant | Market 1036 197,205 / Deep Sea 225,102 | `Kernel.SacarNemesis` |
| :24 | Dragon King (DragonKing) | Twin City 309,248 / Frozen Grotto 6 497,424 | `Kernel.SacarDragon` |
| :30 | Ice Queen (IceQueen) | Twin City 309,248 / Frozen Grotto 2 | `Kernel.SacarReina` |
| :36 | Fat Demon (FatDemon) | Twin City 310,249 / Demon Map 041,040 | `Kernel.SacarGordo` |
| :43 | NemesisTyrant (second) | Market 1036 197,205 / Deep Sea 292,352 | `Kernel.SacarNemesis1` |
| :49 | Thunder Dragon (ThunderDragon) | Twin City 309,248 / Kingdom | `Kernel.SacarTrueno` |
| :56 | Furious Bear (FuriousBear) | Twin City 309,248 / Kingdom | `Kernel.SacarOso` |
| :15/:29/:45 | Top Player contest | Twin City 313,262 | Already `if (false)` before this park. |
| :00 | Free CPs in Dragon City | EventoGMQuest loads 1–17 | Already `if (false)`. |
| :03–:50 | Mr/Ms ConquerHost and ChinaHost claim windows | flags on Thread | Hourly claim toggles. |

World-boss kills paid gold (and previously CPs / Revora Points) from `MonsterTable` SuperBoss drop block.

## Several times a day (disabled)

| Name | Hours | Notes |
| --- | --- | --- |
| Hour of Hunt | 6, 12, 18, 23 | World text only. Claimed 1–100,000 CPs and 1 Slayer Point per kill; no drop-table gate was found. |
| Last Man Standing | even hours | **Still live.** Was 12/18/21 before the 12-a-day change. |
| Horse / steed race | 4, 8, 12, 18, 23 (race); popup 3, 7, 11, 17, 22 | `SteedRace.work`. Prize 100,000 Revora Points to first five. Twin City 289,149. |
| C-O-N-Q-U-E-R | 1, 7, 13, 19 | Twin City 347,249. Prize PaqSagradaRefinada. |
| SoldierSlayer | 3, 7, 11, 15, 19, 23 | Hidden City / Market 1015 441,729. Reborn 0–1 only. |
| Team Qualifier | 11:02, 19:02 | AutoInvite 10562. |
| Pole domination | 19:00, every 15 minutes | Poles on Phoenix, DC, Desert, Bird. 30,000,000 prize pool. |
| Old Power Arena popup | 13:05, 20:05 | AutoInvite 10531 remains disabled. [Restored Arena](POWER_ARENA.md) uses Wang and 13:00/20:00 Pacific sessions; staged, not deployed. |
| Clan War start | 20:33 daily | `ClanWar.Start`. |
| Great Pheasant (Nor.1) | Sun/Wed 1, 4, 8, 12, 16, 20 | Hidden City 1020 526,499. |
| Common PK melee | Retired: legacy 17:00, 22:00 | Admission 900032 and reward clerk 900033 are closed. Popup now follows the closed admission gate. Legacy text advertised super equipment, but reward code selects soul/refinery packs; restoration must reconcile the contract. |

## Daily (disabled)

| Name | When | Notes |
| --- | --- | --- |
| Nobility Kill Tournament | Restored as NobleWar, once daily 15:00 (was 01:05 and 15:00) | King 20k CPs + 2m gold; Prince 12k + 1m; Duke 8k + 500k; Earl 5k + 250k. Dynamic maps from 700. NPC 10620 parked on map 1000. |
| Glory & Fortune | **Deleted.** | No schedule, no popup, no 20:16 kick from maps 146/147. Event NPCs 54259–54262 removed. |
| Legend & Nobility | **Deleted.** | No schedule, no popup. Event NPCs 54263–54266 removed. |
| Dragon Balls | **Deleted.** | No 01:02 invite, no 02:00 kick from map 1354, no Goku NPC 899985. Maze NPC 1155 (SoulToken floor) is not this event and stays. |
| Looting (Saqueo) | **Deleted.** | No 13:00 popup, no 14:00/14:30 map kicks, no chest tick. Clerk 900047 and chest NPCs 900049–900132 removed. ExpeditionClerk 91002 was not this event. |
| Golden Super War kick | **Deleted.** | No 19:01 kick from map 1037. |
| Old native Quiz Show | **Disabled.** | Unverified 2068 gameplay is disabled globally. [Revora NPC edition](QUIZ_SHOW.md) restores weekend sessions through Master Wen; staged, not deployed. No legacy login auto-join. |
| ExtremePK | Sunday 21:02 | AutoInvite 10553. |
| Weekly PK | Sunday 20:05 | Twin City 297,230. |
| Guild War | Sunday 15:00–16:00 | AutoInvite 10515. Flames 14:59. |
| Super Guild War | Wednesday 20:00–21:00 | AutoInvite 10559. |
| Golden Super Boss | Sunday 18:01 | Twin City 227,245. |
| Elite Guild War | Mon/Tue/Thu/Fri/Sun 21:01–22:00 | Twin City 282,161. |
| CP Rain / Gold Rain (PoleTwin) | Monday 10:00–11:30 and 21:00–22:30 | Twin City 288,159. |
| Quarantine | Mon/Tue/Thu/Fri/Sun 20:00–20:30 | `Sistemas.Quarantine`. |
| Class Tournament invite | Monday 20:01 | Twin City 309,145. |
| Champion Arena | Monday and Friday 20:55 | AutoInvite 10549. |
| PK Group | Saturday 12:01 (invite) and 18:56 (Team Elite PK) | Twin City 337,259 / 293,146. AutoInvite 10543. |
| Couples contest | Friday 18:00 | Twin City 696,520. |
| Perfection task | Tue/Thu/Sat 15:00 and 21:00 | Dragon City / Twin City 297,152. Maps 4020+. |
| Elite PK / Heroes | Friday `ElitePK.EventTime` | Hero Clerk in Dragon City. AutoInvite 10533. |
| Nobility pole war | Saturday 20:30, 60 minutes | Map 3842 dynamic. |
| Expedition | Restored. Every 3 hours, 5 min register, 20 min run | ExpeditionClerk 91002 at Twin City 359,295 and on the event map. Cap 16. Solo. Extract after minute 5. |
| Hero of the Game | midnight if CheakUp is ticked | AutoInvite 10563. Map 1507. Not wired from Thread. |

Class PK (Top titles, Monday 22:00) and Class PK2 (Tuesday 22:00) were already commented in `CreateTournaments`. Capture the Flag schedule in `ServerFunctions` was already commented.

## Event NPCs spawned from code (removed)

| NPC | ID | Map | Role |
| --- | --- | --- | --- |
| ExpeditionClerk | 91002 | 1002 358,295 | Live. Expedition event on. |
| LastManStanding | 900000003 | 1002 354,295 | Last Man Standing. **Kept.** Lookface last digit is facing; South (127) faces the plaza. |

Kill-tournament, race, pole, CTF, and guild-war clerks that live in the `npcs` / `sobnpcs` tables are still in the database files. They no longer receive schedule invites. Dialog in `Npcs.cs` is parked in place; do not treat those scripts as live product.

## Source hooks (disabled)

- `Thread.CreateTournaments` no longer constructs KillTournaments or NobiltyPoleWar. ExpeditionEvent.Create is live.
- `Thread.WorldTournaments` ticks Last Man Standing, NobleWar, and Expedition, then returns.
- `SteedRace.work` and `PoleDomination.work` return immediately.
- `Thread.CharactersCallback` invitation blocks for bosses and the listed contests are gated off.
- Glory & Fortune, Legend & Nobility, Dragon Balls, Looting, Golden Super War kick, and Quiz Show schedules/kicks/event NPCs are **deleted**, not parked.
- `Npcs.GetDialog` still routes Last Man Standing, NobleWar, Expedition, Anwen, RevoraMaster, Commander, and Caelan (Soul Trial).

## Rebuild notes

Start from Last Man Standing (`Game/Features/LastManStandingSystem.cs`): `NpcReply.MessageBox` for register (not AutoInvite), a dedicated dynamic map, death = Twin City with no re-entry, claim at a clerk. Do not copy Spanish source dialogue or prize tables blindly.
