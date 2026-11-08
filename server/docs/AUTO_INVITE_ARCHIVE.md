# AutoInvite event archive

Frozen against rollback SHA `a806c8cb3d45c25048fa6ebc69869e9c90cd03b1`.
These were unsolicited client popups (packet 1126 / MsgInviteTrans). They are not part of the new world design. Keep this file; do not turn the popups back on.

## How it worked

- Packet ID 1126 (`PacketID.AutoInvite`).
- Global kill switch already exists: `AutoInvite.Enabled = false` in `Network/GamePackets/AutoInvite.cs`.
- Outbound 1126 is dropped in `Client/GameState.cs` when Enabled is false.
- Inbound accept is ignored in `PacketHandler` → `AutoInvite.Process` when Enabled is false.
- Team.AutoInvite is a different flag (party invite). RouletteAutoInvite is casino table invite. Neither is this event popup system.

Accepting a popup (when Enabled was true) set `Entity.StrResID` to a mode id and teleported or signed the player into that event. Dead players and jail maps 6000 / 6001 / 6004 were skipped by most senders.

## Modes (StrResID) and what they did

| ID | Name | On accept |
|---:|---|---|
| 10515 | GuildWar | Random teleport into Guild City 1038 |
| 10519 | ClassPKWar | Class PK tournament |
| 10521 | WeeklyPk | Twin City 1002 (323, 194) |
| 10523 | MonthlyPk | Twin City 1002 (323, 194) |
| 10525 | HorseRace | Join SteedRace if riding + steed equipped |
| 10531 | PowerArena | Twin City 1002 (336, 137) |
| 10533 | ElitePk | Twin City 1002 (301, 143) |
| 10535 | CaptureFlag | Twin City 1002 (225, 237) |
| 10541 | SkillTeam | Twin City 1002 (313, 143) |
| 10543 | TeamPk | Twin City 1002 (292, 146) |
| 10549 | Champions | Champion Arena signup |
| 10550 | Lastman | Twin City 1002 (308, 262); message pointed at Dragon City |
| 10551 | DeathWar | Twin City death-match desk |
| 10552 | FastScent | Fast scent event |
| 10553 | ExtremePK | Extreme PK |
| 10554 | Terrorist | Kill The Terrorist |
| 10555 | Couples | Couples PK |
| 10556 | Treasure | Treasure box |
| 10557 | DemonBox | Demon box |
| 10558 | DisCity | Dis City |
| 10559 | SuperGuildWar | Map 1234 (224, 237) |
| 10560 | SkyFight | Sky fight |
| 10561 | TwinCityWar | Random Twin City war tiles |
| 10562 | TeamQualifier | TeamQualifier.DoSignup |
| 10563 | HeroOfGame | PK mode + teleport 1507 (96, 113) |
| 10564 | Devastated | Devastated event |
| 10565 | Quarantine | Quarantine (file now empty) |
| 10573 | PlunderWar | Commented out; needed Union |

## Code that sent or handled the popup

- `Network/GamePackets/AutoInvite.cs` — packet, modes, Process teleports
- `Network/PacketHandler.cs` — case PacketID.AutoInvite
- `Client/GameState.cs` — drop 1126 when disabled
- `Thread.cs` — world-tick senders
- `Game/Features/Tournaments/SteedRace.cs` — HorseRace 10525
- `Game/Features/Event/HeroOfGame.cs` — 10563
- `Game/Features/Tournaments/PlanderWar.cs` — 10573 commented out

Related event/tournament implementations (logic can stay; popups must not return):

- CaptureTheFlag, Champion, ClassPK, ElitePK, TeamQualifier, TeamSkill, Qualifier
- GuildWar, SuperGuildWar, KillTheTerrorist, KillTournament, NobiltyPoleWar, TreasureBox, PoleTwin, EliteGuildWar, DominarPole, EventoGMQuest

## New world rule

Do not auto-popup players into scheduled filler events. Future events are designed as part of the living world, not 1126 spam. Roulette/team invite packets are separate and stay unless the owner says otherwise.
