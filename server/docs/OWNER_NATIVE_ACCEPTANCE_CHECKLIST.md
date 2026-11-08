# Revora: your gaming-PC test checklist

Prepared 2026-10-04. Owner decision: **local Revora tournaments only**.
Genuine cross-server transport is outside this roadmap finish scope.

## Before you start

The owner selected the existing production server for acceptance, using the
normal Revora launcher and ports9958/9959. No private VPN or separate test
connection is required. This supersedes the private-connection requirements in
the historical preparation notes below.

**Deployed to production on 2026-10-04 with client patch 1.0.20.** Close the game,
open your normal Revora launcher, install the update and log in on9958/9959.
The main database and changed files have rollback backups. Production startup
completed and the public patch download was hash-verified. Native gameplay
results below remain pending your tests.

[Exact deployment record](research/PRODUCTION_ROADMAP_DEPLOYMENT_20261004.json).
No separate OwnerTest server, database, VPN or client copy is required.

The operator must first provide:

- [ ] Candidate server revision, client patch/manifest version and test server name.
- [ ] A working approved 6711 DX9 client on your gaming PC, connected to that
  candidate through the approved launcher/configuration. Do not change database
  settings or copy old private staging files yourself.
- [ ] Ordinary non-GM accounts: one new character, one equipped established
  character, and enough ordinary participants for the scheduled event being tested.
- [ ] Confirmed event times **in server time**, expected reward amounts from that
  candidate, and which gated features are enabled for this acceptance run.
- [ ] Recoverable production database/file backups and a coordinated restart slot.
  Restarting your client does not substitute for a server persistence check.

Client identity for the operator: approved `Env_DX9/Conquer.exe`, SHA-256
`3728BC3FD4E584D85301EDD2B1887A176267CDAA6356038F9951EF23E4FDED60`,
launched with `blacknull`. The legacy top-level EXE is not interchangeable.

Use **PASS / FAIL / NOT TESTED / NOT AVAILABLE IN THIS BUILD** for each row.
For a failure, record the test ID, character, server time, map/coordinates,
expected result, actual result, and a screenshot or short clip. Do not send
passwords, tokens or database settings. Stop that failing route and continue
independent tests; do not repeatedly spend currency trying the same failure.

## 1. First pass: login, purchases and visible state

| ID | What to do | Expected result |
| --- | --- | --- |
| A1 | Log in, select the server and create a new character. | Enters Twin City, map1002 at327,303; no obsolete newcomer-map detour. |
| A2 | Check the starter item, open it once, then reconnect. | One RevoraStarterPack; no two legacy starter packs. Claimed items and balances persist; reconnect gives no duplicate opening reward. |
| A3 | Walk, jump and change maps near another ordinary player. | Both players see each other and movement updates correctly; no invisible nearby player, stuck map or unexplained disconnect. |
| A4 | Open the Subclass shop at1002:424,339 and buy one relevant subclass item. | Native shop window; visible price matches the exact CP debit; item arrives immediately and remains after reconnect. |
| A5 | Open the Inner Power shop at1002:428,343 and buy Universal Concept A or another listed item. | Buy works; exact CP debit and correct item, rather than silent failure. |
| A6 | Buy one P6 soul at1002:401,339. | Costs2500 CP; correct soul delivered once and preserved after reconnect. |
| A7 | Buy a rune from Vesper with RVPs, then upgrade it using owned +Stones. | RVP display updates immediately. Eligible upgrade consumes the required stones and advances correctly; rune level/equipped state/Prestige remain after reconnect. |
| A8 | In the general mall, test one permitted bound-CP purchase and one regular-CP purchase. | Correct balance is charged; bound purchase stays bound, including supported pack contents. Regular purchase follows its displayed terms. Progression shops A4?A6 use regular CPs. |
| A9 | Test an unaffordable purchase; then a full-bag purchase that cannot stack. | Clear refusal, no debit and no item loss. Distinct confirmed purchases may each charge; do not expect every repeated click to be free. |
| A10 | Equip an earned title and cosmetic wing; open Prestige, reconnect, then inspect again. | Correct title/wing and stats shown; unearned rewards cannot be equipped. A naturally expired temporary award no longer appears usable after refresh/reconnect. Do not change the PC clock. |

## 2. Quest and reward pass

Use the existing earned character and ordinary NPC routes. There is no request
to grind all320 achievements again. Record starting/ending currency and inventory
for each reward claim; allow the required bag space before claiming.

| ID | What to do | Expected result |
| --- | --- | --- |
| Q1 | Complete one ordinary daily quest and claim; reconnect and try the same completed claim again. | Advertised valuable reward, correct binding and immediate updates; no daily-quest P7 and no duplicate reward. |
| Q2 | Complete Moon Box or a selected one-time equipment quest using its normal entry, travel, combat and claim. | NPC text, map, objectives and item names/icons are readable; materials consumed once; eligible one-time P7 rewards remain bound. |
| Q3 | Continue a started quest after reconnect; have the operator restart the test server once with progress saved. | Correct progress and return location recover; no second admission charge, lost owned reward or fabricated victory. |
| Q4 | View the restored permanent mount packs in the mall; buy/open one selected restored pack. | Displayed price2799 CP, correct permanent mount, exactly one debit. Distinct permanent designs increase collection progress; duplicate designs and rentals do not. |
| Q5 | Check the existing Rising Star test character and an ineligible character. | Only legitimately eligible character can own/equip the title. Report displayed achievement count. This observation alone does not prove all320 earning paths. |

For Q5, the operator must identify the provenance of the saved achievement
records. Do not manufacture achievements, delete receipts or grant a title to
make the test pass.

## 3. Combat and timed travel

| ID | What to do | Expected result |
| --- | --- | --- |
| C1 | Observe and attack a scheduled daily boss and a MonsterRush boss. | Long HP bar appears with correct full/max HP and falls with damage. Check HorrifyingSpecter separately for the old10000/5000000 display defect. |
| C2 | Complete a selected timed boss hunt using normal attacks, movement, medicine and any resupply. | Boss pursues/attacks normally; healing and durability permit the route within the displayed deadline. Record elapsed time and resupply trips; a calculator result is not a stopwatch result. |
| C3 | Run Standard Dis City, including the Archer600-kill Hall if using the reviewed Archer route. | Normal movement, target selection, healing, floor admission and final rewards work. Record remaining time and consumables; do not treat higher difficulties as covered. |
| C4 | Join Extraction with another player, find guarded treasure and use an open exit. Interrupt one extraction attempt by moving, then finish a valid hold. | Larger map renders, guards attack, PvP works, multiple exits are reachable; interruption does not pay, completed extraction pays once and survives reconnect. |
| C5 | If testing achievement10525, weaken the late Extraction guardian with the earned bow, remove both bow and arrows, and finish with empty hands. | Ordinary unequip succeeds; final unarmed kill qualifies only if not previously earned. Existing owners should not receive a second achievement. Record actual timing and healing. |
| C6 | Traverse Flame Lit's gate route and one Power Arena timed floor sequence. | Closed gates block until destroyed; ordinary walking/jumping reaches objectives and claims within the event window. No teleport shortcuts or manually advanced clock. |

A PvP loss is not a software failure. Report broken targeting, missing opponents,
invalid admission, stuck returns, incorrect settlement or rewards instead.

## 4. Local tournaments ? schedule these separately

Do not wait through every calendar event in one sitting. The operator supplies
the candidate's registration time and required ordinary roster before each test.

| ID | Competition | What to verify |
| --- | --- | --- |
| T1 | Local CTF | Entry, opposing guild combat, flag pickup/delivery and settlement. Winning contributing saved guild leader receives permanent Conqueror and Radiant Wings; losing/idle players do not. |
| T2 | Local Elite PK, level130+ bracket | Registration, match/round transitions, defeat and return. Placements1/2/3/4?8 receive Legendary/Peerless/Outstanding/Expert respectively for14 days from finalization. |
| T3 | Local regular Team PK, level130+ bracket | Team invites, saved roster, combat participation and return. Placements1/2/3/4?8 receive King Team/Dominator Team/Power Team/Elite Team respectively for14 days; unqualified or absent members receive none. |
| T4 | Reconnect after a claim; operator restarts the candidate once after settlement. | Same reward ownership, remaining lifetime and equipped appearance; no second payment and no renewed expiry. |

These are local Revora editions. No second server or origin-realm test is required.
Other event rewards retain their own documented rules; the14-day rule above is
for these additional titles, not every wing or event prize.

## 5. Features that remain gated

Only test these when the operator explicitly marks them enabled in the deployed
candidate. Otherwise mark **NOT AVAILABLE**, not PASS:

- Elimination: follow [its full ordinary-player checklist](ELIMINATION_ACCEPTANCE.md)
  for herbs, rescue, den, disconnect recovery and the final claim.
- Abyss: [restoration and acceptance requirements](ABYSS_TRIAL_RESTORATION.md).
- Forgotten Realms map1363 / achievement10726: confirm the candidate includes its
  reviewed terrain/client content before attempting entry.

## Send back one result sheet

```text
Candidate/server revision:
Client patch/manifest:
Character(s), class, level, rebirth:
Test date and server timezone:
PASS IDs:
FAIL IDs:
NOT TESTED / NOT AVAILABLE IDs:
Failure ID | server time | map/coords | expected | actual | screenshot/clip
```

A clean first pass does not finish acceptance. The operator must reconcile
failures, complete remaining scheduled/gated checks and verify saved database
rewards. Production hosting alone does not prove correctness; untested rows stay open.

## Operator preparation checkpoint ? 2026-10-04

The private `owner-journal-local-v1` composition now retains the ten-file
Pirate/Tempest bundle references and reconciles five starter journal fields.
It advertises the current bound-P7 one-time rewards and the correct Phoenix
Castle Courier route. The older cosmetic/rune-only `starter-quest-journal-v3`
must not replace these current-source terms. All unrelated journal bytes remain.

Reproduce with `python tools/ClientPatches/compose_owner_journal.py`.
[Exact preparation evidence](research/OWNER_JOURNAL_COMPOSITION.json).
This633769-byte private delta is **not** the complete test package: starter
item/icons, restored collection mall catalogs and server/client configuration
still need coordinated preparation. Do not install the delta independently.

### Combined data payload

The subsequent `owner-data-local-v1` stage combines16 files (34,295,343 bytes):
quest journal/terrain, starter727990 in server and both client catalogs, its two
icon tables, the reviewed monster table, and both restored mount-shop catalogs.
Reproduce with `python tools/ClientPatches/compose_owner_data.py`.
[Validation record](research/OWNER_DATA_COMPOSITION.json).

This supersedes the previous need to combine those data files. It remains a
private payload, not a standalone client download or installer. Server binary,
configuration/dependencies, destination preimage backups and transactional
installation still need reconciliation before the owner test can start.

### Client data installer prepared

`tools/ClientPatches/Install-OwnerTestData.ps1` defaults to read-only `Check`.
It accepts only a separate client copy named `Revora6711-OwnerTest`, verifies the
approved EXE and exact eight source/destination hashes, backs up every original
before replacement, and supports `Apply` and `Rollback`. A caught partial apply
restores changed files automatically. Unknown files are refused rather than overwritten.

[Installer verification](research/OWNER_CLIENT_INSTALLER.json) covers apply/rollback,
locked-file recovery and refusal of a changed file or the active client path.
A hard interruption with a leftover temporary file needs operator review before
rollback. This is not a signed patch or native gameplay acceptance. Do not apply
until the operator supplies the matching server and test connection.

### Matching private server prepared

The matching reviewed server executable and eight server data files are staged
in the existing isolated candidate. Two actual game/database startup/shutdown
cycles pass, preserving33 owned tables; candidate and database are stopped.
[Exact server/data hashes and evidence](research/OWNER_CANDIDATE_STARTUP.json).
This supersedes the earlier pending server-binary/dependency preparation note;
normal startup is not logged-in player or native acceptance.

The owner confirmed normal public access through Revora Launcher / Conquer-Revora
on ports9958/9959, receiving the same patches as other players. No private test
connection is established. The candidate listens only on loopback19558/19559/19619
when started; the normal launcher still reaches production, not this candidate.
Do not point the patched client at production. No firewall, public listener,
launcher endpoint or live server was changed. Candidate acceptance requires a
separate test connection and matching client copy; normal live play cannot count
as acceptance of the staged candidate. Here, local tournaments means competitions
within one Revora realm, not a server already running on the owner's gaming PC.
