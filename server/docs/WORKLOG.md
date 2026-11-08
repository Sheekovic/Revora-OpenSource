# Work log: Revora 6711 3D

This is the donor server at `Source/Revora-6711`. Target client: genuine 6711 DX9 (`Conquer.exe blacknull`). Platform: C# / .NET Framework 4.8, Debug|x86, output `bin\Debug\Revora.exe`.

## Wall of Prestige correction (2026-09-13, deployed)

- Owner wall.png exposed currency-based self/rank values and a missing showcase.
  Unified 3253 categories, personal prestige, overall/class pages and winners
  around the existing table-based calculation. Corrected its hardcoded two
  rebirths to use actual rebirth count. Other category formulas are preserved.
- Confirmed 1151 winner mode and 3257 repeated appearance records through bounded
  static inspection of the exact approved DX9 hash. Recorded addresses and field
  evidence in PROTOCOL_RESEARCH.md before activating the writers. The client is
  unchanged. Restored full ten-row capacity and accurate counts/class categories.
- Reconstruct offline scores from durable entities/items/subclasses plus loaded
  cultivation/nobility data in a read-only, repeatable-read transaction. The
  loader never calls character login, item-expiry deletion or currency setters.
  Real online profiles refresh on queries/save; offline entries survive logout.
  Synthetic actors are excluded; query bursts are bounded per session.
- The read-only harness loaded both saved characters and repeated the load with
  identical ordering/totals. The first harness connection-options attempt was
  rejected by this Npgsql version before connecting; the production loader now
  explicitly enforces the read-only transaction. No schema or balance changes.
- Final isolated build passed 55 prestige and 29 CharacterInfo regression checks.
  Backed up prior executable/symbols under
  `bin/Debug/deployment-backups/prestige-wall-20260913-221640`, gracefully stopped
  PID 3768 and restarted hidden as PID 8004 with runtime configuration preserved.
  Installed SHA256:
  `0964A4F11EACCA861F7FEE9DFE1C3195B25FD4A324A0AC5C5BECCBD00C5D88E6`.
- Account 9958 and Game 9959 passed initial-handshake checks; no new exception
  files appeared. Visual wall rendering and authenticated save/relogin remain
  unverified. Offline equipment inspection returns the confirmed client message;
  it does not claim an offline equipment-detail feature.

## Character status correction (2026-09-13, deployed)

- Compared the owner's status-debug.png and modern Revora's recorded exact
  6711 receiver evidence. Corrected initial CharacterInfo HP to UInt32 at 72,
  mana to UInt16 at 76, and CP/hair field widths. Populated display identity at
  140 with the durable character UID to address Domino's Code display error.
  Saved attributes and the existing prestige calculations are unchanged.
- Isolated build succeeded; 29 actual-assembly status checks passed, including
  large HP, adjacent-field preservation, stable identity and subsequent updates.
  An initial fixture attempt encountered the CP setter's unconditional database
  write and failed during dependency loading; the fixture now initializes its
  backing field directly and the successful run requires no database.
- Preserved the running executable and symbols in
  `bin/Debug/deployment-backups/character-status-20260913-214655`, then gracefully
  stopped PID 6948 through the PID/path/hash-bound console helper. Restarted
  as PID 3768, preserving runtime configuration and player data. Installed SHA256:
  `5851195F579697BE6EDEB71C8DE447D9F130D740ABB75E7D765B412A51FDEC54`.
- Both Account 9958 and Game 9959 returned initial handshake data; no new startup
  exception files appeared. No authenticated client session or visual status
  check was automated. Reconnecting is required to receive corrected CharacterInfo.

## Companion jumping regression correction (2026-09-13, deployed)

- Owner reported companions only walking after the AI deployment. The new
  MoveToward path had replaced all jump decisions with two walking steps.
- Restored jumps when at least five tiles from the destination, capped at six
  tiles per decision. Every intervening tile and diagonal corner is checked;
  obstructed jumps fall back to cached walking paths. Existing jump packet
  serialization is shared unchanged with WorldActorMotion's earlier jump path.
- Build passed 57 companion checks and 25 gold checks. Backed up the current
  executable/symbols to `bin/Debug/deployment-backups/companion-jumps-20260913-212145`.
  Gracefully stopped PID 3644 and restarted as PID 6948 with configuration and
  data preserved. Installed SHA-256:
  `4E75B829C47680A40DC97444726CA289E8D4BDEF51300CE800B261BCE737B4AE`.
- Both 9958 and 9959 passed listener and initial-handshake checks; no new
  exception files were observed after startup. Actual client jump animation
  observation remains with the owner; no authenticated session was automated.

## Gold and companion deployment (2026-09-13, owner approved)

- Owner requested "build and restart". Rebuilt current source into the isolated
  output; its executable matched the build that passed 25 gold and 51 companion
  checks: `D4A4E96A868B0DEE501841522C052C26B57F290CF2C5A94560F8BB09435D7E7D`.
- Backed up the former executable and symbols to
  `bin/Debug/deployment-backups/gold-companions-20260913-205715`, with a hash
  manifest and rollback instructions. Preserved the earlier cultivation backup.
- Gracefully stopped PID 2900 through `@close`. The console helper was bound to
  that PID, intended executable path and prior executable hash. Its first x86
  attempt failed before sending input because the server was a 64-bit process;
  the reviewed helper was rebuilt as x64 and then completed successfully.
- Replaced only Revora.exe and Revora.pdb; verified runtime configuration hash
  unchanged. Started the updated server hidden as PID 3644 at 20:58:38 UTC.
- Account 9958 and Game 9959 are owned by PID 3644; both accepted a connection
  and returned initial handshake data. No new exception files appeared in the
  startup check. No credentials or authentication packets were sent by probes.
- Real-client gold collection, UI updates and companion behavior still need
  in-game verification. This deployment does not claim those checks passed.

## Auto-hunt ground gold (2026-09-13, staged before deployment above)

- Confirmed auto-hunt toggled state without any automatic collection path;
  manual pickup required the character's exact tile.
- The existing one-second screen item callback now collects up to eight eligible
  gold piles within three terrain-visible tiles. Existing VIP 1 eligibility,
  owner protection, team money sharing, expiry, trade and map gates remain.
- Manual and automatic gold collection share per-character/per-drop locking and
  atomic removal of the exact map entry, preventing replay and double credits.
  Overflow leaves the pile on the ground. CPs and equipment are not auto-collected.
- Gold credits use the existing Money update and heartbeat/logout persistence;
  no schema, configuration or client changes. Translated the touched auto-hunt
  eligibility message into English.
- Isolated Debug/x86 build and 25 actual-assembly pickup tests passed.
  Executable SHA-256: `D4A4E96A868B0DEE501841522C052C26B57F290CF2C5A94560F8BB09435D7E7D`.
  This build includes the earlier staged companion AI update. No live executable
  or player data was replaced. Client display and durable logout/login checks
  remain pending approved activation. See `tests/AutoHuntGold/README.md`.

## Companion AI first pass (2026-09-13, staged)

- Added cached terrain pathfinding with 256-node search budgets, failed-route
  backoff, diagonal collision checks and at most two walking steps per decision.
- Added assist, protect, passive, hold here, stay back and retreat whisper orders;
  owner attack intent comes from the completed existing damage handler and
  expires after six seconds. Targets are leashed, prefer current targets when
  priorities are otherwise comparable, and exclude other players' engagements.
- Removed random skill-effect selection over generic damage. Weapon attacks use
  shared damage calculations; Water uses actual Cure/Thunder and Fire uses
  Tornado. Added costs, cooldowns, ranged spacing, Warrior threat interception,
  low-health retreat and bounded resource regeneration.
- Serialized commands and decisions per actor, made ownership checks atomic,
  prevented overlapping ticks and unlimited hired-actor replacement spawning.
  Added safe map-transfer landing checks, delayed recovery outside combat, held
  anchors, event exclusion and cleanup on death/logout. Revival placement now
  uses the previous position on the current map instead of home coordinates on
  an unrelated map.
- Synthetic equipment skips durability persistence. Synthetic monster killers
  skip player XP/quest-credit handling; existing no-drop policy remains.
- Added `tests/CompanionAI`: production navigation source plus actual compiled
  game handlers against in-memory maps and non-GM character fixtures. See its
  README for reproduction and the exact scope of validation.
- Reused `obj/chi-refill-validation` for the isolated build. Running executable,
  client, runtime configuration, dependencies and player data were not replaced.
  Genuine-client animation, update delivery, materialization/revival and live
  population/load checks remain outstanding. This update is not yet activated.
- Validation: Debug/x86 build passed, 51 companion checks passed, prior 12 Chi
  and 19 Jiang Hu checks passed, and `git diff --check` passed. Tested executable
  SHA-256: `1E9FC552D4B33C630D94D9186791F625135E65A39DE9CB627FF998CAB510E62C`.

## Chi refill price (2026-09-13)

- Replaced the old 100,000-CP empty-to-full refill with 1,000 CPs through
  `ChiFullRefillCp`, matching the official published rate of 1 CP per 4 Chi:
  https://co.99.com/guide/guides/chi_system.shtml (checked 2026-09-13).
- Partial refills round up to a whole CP. Full or over-cap balances are left
  unchanged without charge. Purchased Chi is saved immediately and sent to
  the client. The existing low-Chi study refill uses the same handler.
- Removed an unused SQL Server SMO import from `Accounts.cs` that prevented
  this checkout from compiling without that legacy library.

## Jiang Hu training prices (2026-09-13)

- Replaced the flat 50,000-CP paid-training charge with the official 10-CP
  increments, capped at 500 CPs. The existing training counter is retained and
  saved after purchase. Insufficient-funds text now reports the actual price.
- Restore costs 20 CPs, including when the player has exactly 20. The old
  attribute is copied before rolling and consumed after restoration, fixing
  the shared-reference bug and repeated restore charges.
- Free training consumes 10 Study Points, one Talent and one Free Course.
- Reject short training requests, zero/out-of-range stars and stages, locked
  stages and trading sessions before payment. Training and CP restoration
  share the character's Jiang Hu lock.
- Official sources and confidence are recorded in `PROTOCOL_RESEARCH.md`.
- Validation: isolated Debug/x86 build passed. The scripts in
  `tests/CultivationPricing/` passed 12 Chi cases and 19 Jiang Hu assertions,
  including concurrent paid requests and restore snapshot behavior. Database,
  socket and stat dependencies are stubbed; no live-client or database-failure
  verification was performed during the isolated tests.
- Owner approved deployment on 2026-09-13. The existing process exited through
  its `@close` console command. Its executable and symbols were backed up under
  `bin/Debug/deployment-backups/cultivation-20260913-194241/`, then replaced with
  the validated build. Configuration and database files were not replaced.
  SHA-256 of deployed `Revora.exe`:
  `8376655F7A4F0D53AB0A11CFB75154260F447E0F36CA5D06F7F2913F88A4E0EA`.
  After restart, both Account 9958 and Game 9959 accepted bounded connections
  and sent initial handshake data. No credentials were used or packet bodies
  persisted. Player login and live cultivation purchases remain to be checked.

## City life

- City guards rebuilt as Fake player-shaped actors (UIDs from 1100001), not monster chickens.
- Living-world adventurers rebuilt the same way (UIDs from 1200001), names `[Adv] …`.
- Spawn hubs: Twin City 1002, Desert 1000, Phoenix 1011, Bird 1015, Ape 1020, Market 1036.
- Genuine 6711 player meshes: female 2562005, male 371006. `PaintLook` does not use Face/Body setters for mesh.
- Adventurers wear Super Tempest Wing 204009 +12 and show Chi + Jiang Hu on packet 10014.
- Regional hunt: adventurers 12 tiles, guards 18 tiles. Combat range 2. Monsters can target guards and adventurers; Fake `Send()` is a no-op so world combat uses `Kernel.SendScreen` / broadcast helpers.
- Monster drops skip loot when the killer is a guard or adventurer.

## Companions without a model

- Whisper intercept in `Chat.cs` before Fake `Send()` swallows the line.
- Canned hire / follow / wait / dismiss. Guards are not hireable.
- One companion per player. Follow across maps by snapping near the owner. Dismiss on logout. No minted CP, no stolen loot.

## RevoraMaster and economy desks

- NPC **900000001** Twin City 410,354: peak, vestments, rename, mount kit.
- Mount pack jail was hitting Bless 7 and Glory Tower gems. `IsLegalRevoraTalisman` in `ItemHandler.cs` makes the sold kit legal for **everyone**.
- Prizewell **900035** Twin City 396,342: Revora Points. C# and database column are `RevoraPoint`.
- Combined rebirth NPC Sereth **59558** Twin City 395,362. Second-rebirth options 170–179. `Reborn` / `FirstRebornClass` / `SecondRebornClass` persist in setters because Class/Level were saving first and `SaveEntity` could fail mid-spell loop.
- ChiJiang Clerk **900043** Twin City 396,348: leftover 1,000,000 CP / level 140 second-reborn lock removed. Walk-up is 5,000 CPs, level 110, first rebirth. English desk and Twin City exits. Beasts renamed Chi Beast / Jiang Hu Beast.
- Twin City plaza sweep: leftover event clerks jailed to map 6000 with closed handlers. ChiJiang moved to the cultivation column. MillionaireLee to the service row. Boxer next to the arena. SolarSaint kept as a closed Perfection notice.
- Khopesh blade **410443–410449** (type 410, level 110). Super sold at Vesper for 5,000 Revora Points. Hooked 430 look. Ordinary players.

## Stability after rename

Rename used to set `OnDisconnect`, crash on a missing clan/guild member, then block relog through `DisconnectPool`. It also wrote `PreviousMapID` into `PreviousY`, which produced off-map Y values and a black screen after a Twin City scroll.

Fixes:

- Apply the new name immediately, table by table, with per-statement try/catch.
- Park the character at Twin City 1002 (300, 278) before disconnect.
- Save `PreviousMapID` into `PreviousMapID`.
- Login `SetLocation` always sends the map packet even if flowers/clan setup throws.
- Teleport `MapStatus` looks up BaseID when the dynamic ID is missing from `maps`.

## Other operator-facing changes

- `@level` and Taoist Water/Fire training paths.
- 60-second heartbeat `SaveEntity`.
- Nobility `DuracionRey` handling.
- Facebook / ExtremeTop vote removal.
- Spanish NPC dialogue pass to English.
- Global auto-invite disable (`AutoInvite.Enabled = false`, drop packet 1126, skip MessageBox callbacks).
- Bot-jail map 6004 remains for real enchanted/blessed junk. The RevoraMaster mount kit is not junk.

## What this repo is not

- Not the modern `Revora.Host` / PostgreSQL stack.
- Not a public bind or production publish.
- Not an LLM on the 700 ms world tick.
- HeadAbbot was not changed unless a later task says so.


## 2026-09-13 ? progression shops, Vesper runes, patch 1.0.8

Added native CP shops at 1002 (424,339) and (428,343), covering 11 subclass items and all 17 configured Inner Power books. Replaced Vesper's Khopesh category with 33 level-1 runes at 500/750/1000 RVPs. Purchases and study books use atomic currency/item persistence; saves share the session item lock. Fixed Modesty Book fall-through and Cordiality Book use. See PROGRESSION_SHOPS.md for the full catalog.

Validation: 39 purchase/book/dialogue checks in a disposable PostgreSQL schema, 55 prestige checks, 29 character-status checks, byte-preserving client catalog verification, actual NPC insert/rollback verification, and live Account/Game handshake probes. No new exception files after startup. A first deployment attempt hit the NPC name varchar(16) limit and automatically restored originals; InnerPowerShop fits the existing schema and the second deployment succeeded.

Live server PID 8088, started 2026-09-13T23:06:17Z, executable SHA256 B63B39DAE8D7F92F14103E52C948D92BF57903BC0D6D27D61641D0B594952C90. Rollback: bin/Debug/deployment-backups/progression-shops-20260913-230436 plus the ignored subclass-shop/originals catalog snapshots. Patch published to C:/Revora/Web/wwwroot/download/1.0.8.zip (3,130,605 bytes), SHA256 df77d646115d5b77d47eb38579583b63fc6bf3de6c42570e927d910b704060ef. Public catalog and downloaded bytes verified. Client visual interaction remains untested; restart the client after updating.

Owner additionally requested site news. Published https://playrevora.com/en/news/patch-1-0-8; featured it first on news/home and in Atom. Web article/index/feed all returned HTTP 200. Rebuilt only Web against the deployed Revora dependency assemblies; replaced Revora.Web.dll through its existing scheduled task. Website DLL SHA256 12017B203C38AD3C9321EF31E36BF997031C96ADCC1E9A16E36EBFBC3163911D. Website backup: C:/Revora/Backups/Web-news-1.0.8-20260913-231428.

### 2026-09-13 - Native shop silent Buy workaround, patch 1.0.9

- Found the exact approved DX9 native NPC purchase path hardcodes silver-mode
  validation, rejecting 26 zero-silver-price items despite CP shop display.
- Published data-only 1.0.9 and applied it to the local 6711 client: exactly
  26 client silver-price fields changed 0 -> 1; actual CP prices unchanged.
  See SHOP_BUY_6711.md for static addresses, hashes, rollback and limitations.
- Carry one silver per requested unit of a corrected item; no silver is charged.
  This workaround retains native silver affordability and is not a complete
  CP currency UI adaptation. Actual client Buy clicks remain unverified.
- All 39 ordinary-player server purchase/persistence checks passed. Public
  latest.json advertises 1.0.9; downloaded archive matches SHA-256
  6f6e0240b95ce5cad797c8d8e266592d733a342005ecb8f14277dc2482c0b9bb.
- Preserved 1.0.8 and local preimages. Game PID 8088 and B63B39DA... executable
  remain unchanged; no restart, NPC mutation, balance or server catalog edits.

### 2026-09-13 - Published patch 1.0.9 website news

- Published https://playrevora.com/en/news/patch-1-0-9, featured first in news
  and added to Atom. Updated 1.0.8 with a hotfix notice and current download.
- Copy explains the 26 affected items, unchanged CP prices, one silver per
  corrected item requirement, and closing/updating/restarting the client.
- Web-only build passed with zero warnings/errors against deployed dependencies;
  rendered article/index/home/feed checks passed. Public article/index/feed all
  returned 200, and the prior article includes the hotfix link.
- Replaced only Web DLL D45DE9ED710ED7805240835710F19869F37F5DD538C0D5A080F9A1DC6DA12DBA.
  Backup: C:\Revora\Backups\Web-news-1.0.9-20260913-234128.

### 2026-09-13 - Owner-supplied patch 1.0.10 website listing

- Owner archive already existed at C:\Revora\Web\wwwroot\download\1.0.10.zip.
  CRC verified; single entry ini/GameAnnounce.ini. Preserved supplied bytes.
  SHA256 b0e3e73cdc6ba81756e00e7a99e400374fa5047c7528d1aaff413113af39d454.
- Published /en/news/patch-1-0-10 as Welcome Announcement, featured first on
  news/home and in Atom. Linked prior shop notes; explained this ZIP contains
  only the announcement and launcher installs earlier patches too.
- Told owner announcement still references Vesper's old Khopesh reward;
  did not silently edit the supplied immutable patch.
- Warning-free Web build; local article/index/home/feed checks passed;
  public article/index/feed returned 200. Latest catalog advertises 1.0.10,
  downloaded patch hash matches owner archive. Changed only website DLL.
- Web backup C:\Revora\Backups\Web-news-1.0.10-20260913-234832.

### 2026-09-13 - Potency Hunt deployed for owner playtest

- Map 1217 now maintains 200 additional PotencySerpents near (561,549).
  Owner baseline: AlienSerpent. HP 43,813, attack 1,035, defense 275;
  same level/model/attack interval. Respawn about 1�1.5 seconds.
- Final owner-selected mutually exclusive drop odds: Outstanding 50% (400),
  Excellent 25% (600), Supreme 10% (1,000), empty 15%.
- Added PotencyGuide at Twin City (432,347), free entry, and field guide at
  (559,547), free return. Both explain packs, rates and the book/Potency split.
- Hardened farm-pack pickup/use against replay and incorrect ownership;
  committed cumulative awards recover across restart/login checkpoint gaps.
  New additive revora_potency_awards table; InnerPower.txt appends applied
  award totals and uses atomic file replacement. See POTENCY_HUNT.md for
  rollback restrictions and remaining legacy Union Fund checkpoint behavior.
- 34 Potency Hunt fixtures and 39 progression-shop regression checks passed,
  including ordinary-player guide entry/return, real cached map placement,
  actual death processing, exact roll boundaries, SQL failure and recovery.
- First close attempt changed no server files: copied helper still attached
  to the old console PID. Corrected that PID and reused the original backup.
- Graceful deployment: old PID 8088 -> new PID 6852, started
  2026-09-14T00:25:20.7370063Z. Game EXE SHA256
  2D18CA00A148CFE02527553DEAD9D5A1F3DB8E4FD2A323B9BC68875D30B13D40.
  Both 9958/9959 handshake checks passed. Repeated live status showed tracked
  200, alive 200; no new exception files during post-start observation.
- Backup: bin/Debug/deployment-backups/potency-hunt-20260914-002343.
  Preserved pre-deployment InnerPower.txt after graceful save.
- Published https://playrevora.com/en/news/potency-hunt, featured in news and
  Atom, linked from the original shops article. Public article/index/feed 200.
  Web DLL 957DF4E88B6266370B3A39EFBF64B419174EDE07EDD68613DC5E14A9D5CD2DFE;
  backup C:\Revora\Backups\Web-news-potency-hunt-20260914-002809.
- Existing client assets suffice; public client patch remains 1.0.10. Combat
  feel and client visual presentation are ready for the owner's in-game test.

## 2026-09-13: brief PotencyGuide, rarer packs, larger flocks

Owner requested short dialogue that belongs in the game world. PotencyGuide now gives one short greeting with Take me there./Take me home. choices; arrival messages are brief. Drop tables and population details stay on the website.

PotencySerpent target increased from 200 to 1,000. One exclusive roll now yields 10% Outstanding, 5% Excellent, 1% Supreme, or 84% no pack. Strength, pack values, and respawn delay stay the same. Existing reachable spawn grid has sufficient capacity without changing bounds or spacing. Status JSON derives its target from the population constant.

Pheasant max_per_gen is doubled once at spawn load for the exact Pheasant family, leaving database baselines intact. Twin City flocks at (552,235), (665,313), (444,480) go from 100/40/100 to 200/80/200: 480 total. Startup logs report target and placed counts per flock. No repeated multiplication across restarts.

Validation: server build succeeded; all 36 Potency Hunt checks passed, including 1,000 real-terrain placements, exact 10/5/1/84 histogram, 480 distinct Pheasants and per-flock counts, normal-player guide travel, pickup/use and award persistence. Initial expanded fixture lacked its timer pool; supplied an unstarted fixture pool and reran successfully. Website build passed with zero warnings/errors using deployed dependencies; article, homepage/news placement, and Atom preview passed.

Graceful game deployment at 2026-09-14T01:19:18.7969964Z, PID 3504. EXE SHA256 E3C4E59D1FB69986B27D6B4273BC8EAC3954C2C1DCF042D7B5C594EA256106BD. Both existing Account/Game listeners and initial handshakes passed. Live status after restart and again at 01:20:06Z reported target/tracked/alive all 1000. Backup: bin/Debug/deployment-backups/potency-hunt-tuning-20260914-011903. Existing player data, pack ledger, and checkpoints retained.

Published revised https://playrevora.com/en/news/potency-hunt with 1,000 population, new rates, and matching guide choice. Public article, index, and feed returned HTTP 200. Web DLL SHA256 72190C5C22551A4F141705F85BCAC97D73DA904EEE5CC2A09D667CF2E0C91205; backup C:/Revora/Backups/Web-news-potency-tuning-20260914-012001. No client patch required. Native-client visual/combat playtesting was not performed.

## 2026-09-13: verified latest boss-reward commit 2c31af9

Reviewed 2c31af98855627e80ff16f303a7c369d0dba158a (WhiteDragon, GreatPheasant(Nor.1), SlayerSoldier purse/pack removal) and confirmed the deployed assembly already contains the change. No server redeployment was necessary. Ordinary-player fixtures exercise actual installed boss templates and MonsterInformation.Drop, with normal random money/item drops disabled in the fixture to isolate retired special rewards. They verify base kill CPs, disabled CP rewards, persisted CP balances, no retired gold/RVP/pack dumps, and SlayerSoldier summon-state cleanup. Existing terrain, loot, guides, pickup and persistence checks also pass: 49 total. Tests use the disposable schema only; no live player balances were changed.

Audit assembly is an exact copy of the running deployment: SHA256 C9E1F78EE6831C1F4B94E84362504398740131A21E41A411CEDA72AA0CA0B356. Harness/results are under obj/chi-refill-validation/latest-commit-audit. Live process 1632 started 2026-09-14T01:28:11.1554087Z; exact executable path verified, Account/Game handshakes passed on existing 9958/9959 listeners. Fresh status at 01:32:37Z reports target/tracked/alive 1000. Real-terrain fixtures confirm 480 Pheasants (200/80/200), Potency odds 10/5/1/84, and ordinary guide entry/return. Only regression fixtures and this audit record changed during review; no gameplay correction was needed.

## 2026-09-13: HorrifyingSpecter missing HP bar investigation

Confirmed actual server boss flag/full spawn HP and traced approved DX9 boss selection to an exact-name client profile lookup. Missing HorrifyingSpecter profile fails that gate; existing family 2719 is CreepySpook. Prepared and validated data-only 1.0.11 with the new alias and matching 5,000,000 HP, preserving all existing profiles. No live file edits or release occurred: automatic approval review rejected apply/publication pending explicit approval of this public patch. See BOSS_HUD_6711.md for evidence, hashes, validation and recovery.

### Boss HUD patch published after owner retry authorization

Owner explicitly requested retry after changing permissions. Applied staged monster.dat to the specified local 6711 client and published immutable 1.0.11.zip at 2026-09-14T02:30:35.0461923Z. Public downloaded SHA256 matches b2c5ca8ee6d3bfcf77bfc498e286fde62c2c635e24c1c116c05799ccf7742232; launcher catalog reports 1.0.11. Original client data is backed up. First authorized attempt hit PowerShell null-string conversion in File.Replace before changing the active file; corrected to NullString.Value and reused the verified pending bytes. Server was not changed. Full client restart is required; native visual check remains for the owner.

### 2026-09-13: owner confirms HorrifyingSpecter HP bar

Owner confirmed seeing an HP bar after the 1.0.11 client-profile correction. Native-client HP-bar visibility is now owner-verified.

## 2026-09-13 — Daily bosses and actual boss HP
Implemented four persistent random-minute daily hunts, two-hour expiry, 100M HP, roughly 7K player hits, exact-once killer currency payments, and four transactional P7 ground drops. Corrected the owner's 10000/5000000 report by sending actual HP in both spawn and health attributes. Staged patch 1.0.12 with daily and Boss Rush name profiles. Build succeeds; 143 isolated schema/real-terrain checks pass. See DAILY_WORLD_BOSSES.md for behavior and validation limits.

Daily-boss deployment completed at 2026-09-14T03:11:04Z after the owner closed the previous server. Live PID 5048, assembly B75B1ADD162C3D716EE4F89B52192714DFF7B10B25203B7CA92E6FC110B1F344. Both listeners and fresh 1000-Potency/daily-schedule status verified. Patch 1.0.12 locally applied and HTTPS catalog/download hash verified. Original console did not consume the queued close command; no forced termination was performed. Next first hunt: Nightveil King at 21:42 Pacific on 2026-09-13.

## 2026-09-13: Extraction expansion and event polish

Owner selected PvP/PvE with guarded treasure and multiple exits, then authorized improving other events, updating Git and uploading daily-quest ideas. Implemented the isolated Bird Island Extraction field (3,816 reachable placement samples, 16 separated entries), 72 scouts, three guarded caches, three staged ferrymen, a late Specter hunt, protected satchel and atomic item/RVP extraction claims. PK mode restores on exit. Relic Rush gets final-minute double points, carrier coordinates and missing-relic recovery. Wave Defense gets an eight-second break, one-time recovery, post-health HUD and surviving-post targeting. Added DAILY_QUEST_IDEAS.md as an explicitly unimplemented design proposal. All eight validation suites pass: 418 checks total (182 combined event/boss/Potency, 39 shops, 57 companions, 25 gold pickup, 12 Chi, 19 Jiang Hu, 29 status, 55 prestige). See EVENT_IMPROVEMENTS.md for limits.

Final event deployment completed at 2026-09-14T03:43:28Z. Live assembly SHA256 1C0CE216C7F70725A86F9B99F0288AC54C10223B4E805E474D0697CCB07BD981, PID 8304. Native startup creates a hidden dedicated console with non-inherited handles. The normal @close command worked for both event deployments; Windows-close fallback was not needed. Account/game handshakes passed, Potency stayed at 1000, daily random minutes survived restart, and Extraction initialized as instance 11019/base 1015 with 3816 reachable placement samples, 16 entries, three exits and three camps. A final correction reused the existing relic model for caches and retained configurable extraction reward values. Backups and deployment manifests remain ignored under bin/Debug/deployment-backups and obj/chi-refill-validation/events-release.


## 2026-09-14: daily boss locations and missing bars

Moved Moonfang King to 1002 (438,664), Dawnfire King to 1011 (748,491),
Sunscar Tyrant to 1020 (616,507), and Nightveil King to 1000 (400,500).
All four exact centers pass the cached-terrain spawn fixtures. Expanded
health serialization checks to full/half/1/0 for every daily boss; the
combined Potency/boss/event suite passes 210 checks.

Owner confirmed Sunscar lacked a bar even after patch 1.0.12 and a full
client restart. Static inspection and a bounded, hash-gated x86 native
fixture established that GameData truncates literal-space section headers.
Patch 1.0.13 encodes those spaces as `~`, preserving displayed names.
The actual native loader reproduces all nine missing daily/Boss Rush
profiles before the fix and resolves all nine with Boss=1 and correct HP
after it; HorrifyingSpecter remains valid. Rendered bars still require an
in-game check. See BOSS_HUD_6711.md for evidence and reproducible tooling.

Patch 1.0.13 published and locally applied at 2026-09-14T23:34:30Z. HTTPS
catalog and archive hash verified. Server deployed with backup and graceful
shutdown at 23:34:35Z, PID 8912, assembly SHA256
40FBA55813291E04B0381B4F0C3CF027059353A850F5DA2F9012E3F1500C1D4F.
Both account/game handshakes passed; the four daily plans and defeated
Sunscar state survived; Potency remained at 1000 and Extraction initialized
with its existing layout. Ignored server/client deployment records and
original client bytes are under obj/chi-refill-validation/boss-hud-space;
server backup is bin/Debug/deployment-backups/boss-locations-20260914-233429.


## 2026-09-14: rune +Stone upgrades and persistent prestige

Fixed the disabled inventory-material branch in rune composition. +1..+8
stones now use the approved 6711 EXP values, level safely to blue 27/yellow 9,
and atomically save the rune plus material consumption. Bound materials bind
the target; malformed inputs, wrong owners, locked materials and max-level
targets consume nothing. Repeated packets are suppressed for one second because
the native builder supplies no unique request ID; later deliberate stack uses
remain possible. Equipped upgrades refresh skills/stats; login reconstructs
rune skill levels from saved rune items.

Added native prestige categories 21/22/23 and equipped rune snapshots to both
online and saved-player scoring. Verified every score and EXP row against
installed 6711 tables. Rune bag return now removes the duplicate in-memory
collection entry, and equip operations reject invalid color/slot combinations.
Build and 79 checks pass (21 rune/SQL fixtures, 58 prestige regressions).
See RUNES.md and PROTOCOL_RESEARCH.md for scope, evidence and validation limits.

Deployed at 2026-09-15T00:00:58Z, PID 4072, assembly SHA256
BFB8E4BAD934691BDF70B3D25F37BBE69269962938AF6F99988ABEE0DA8ADCE7.
Account/game handshakes and current Potency/daily/Extraction status verified.
Backup: bin/Debug/deployment-backups/runes-20260915-000047.
No client patch or schema migration. In-game UI confirmation remains pending.
