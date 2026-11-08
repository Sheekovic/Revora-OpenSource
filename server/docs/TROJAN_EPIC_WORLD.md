# Trojan Epic private-scene prerequisite

Implemented 2026-09-21. `TrojanEpicScenes` provides owned room allocation,
actor placement, guarded transfers and cleanup. The candidate server lifecycle
now initializes the complete quest/training/journal coordinator and schedules
cleanup. The running server has not been replaced. Genuine-client and full Host
restart acceptance remain unfinished; this is not a live availability claim.

## Terrain and actors

The local client and server evidence is recorded in
[the research baseline](research/TROJAN_EPIC_BASELINE.json). The native journal's
3830–3832 bindings are unresolved. Revora uses the available epic terrain 3825
for three private room layouts; this is an explicit adaptation, not a verified
reconstruction of retail Olden Twin City.

The 96×96 server grid is checked against SHA-256
`459DF7EB9716F1380A06177B0B20ADBC058324421E1FCFDB818416F70CFA6A32`
before allocation. Runtime walking checks then reserve actual NPC footprints and
require a reachable approach within four tiles of every actor and a clear
combat tile. Landing is 45,45; the reserved combat tile is 45,40. Catalog NPCs,
monsters and portals are not inherited into the private map.

| Story phase | Actor IDs and coordinates |
| --- | --- |
| Arrival through traitor | General Pak 10581 at 34,54; Monk Misery 10583 at 54,34; Mr. Mirror 10582 at 40,35 |
| Essence extraction and infusion | General Pak 10600 at 34,54; Divine Jade 10602 at 54,54 |
| Borrowing through pending reward | General Pak 10603 at 34,54 |

Looks 29216, 29236, 29226 and 29256 resolve to the installed General Pak,
Monk Misery, Mr. Mirror and Divine Jade profiles. Navigation content still needs
to replace the journal's original coordinates/map bindings for this adaptation.

## Ownership and lifecycle

Creation requires the canonical eligible character, its owner lock and its exact
saved story stage. A character can reserve only one scene, with a global cap of
32 and a thirty-minute lifetime. Each room gets its own dynamic ID, floor and
map-owner metadata. Creating or walking a room grants no story progress.

All four existing Entity teleport entry points check Trojan scene admission.
Foreign players and owners without a server transfer cannot enter. Existing
owners can make bounded local moves to clear tiles. Expiration immediately
revokes scene ownership. Arrival and normal return are saved; return selects a
clear tile near the installed Trojan Star (10022) in Job Center (1004).

The ordinary entity loader already sends non-housing private-map logins to Twin
City. That behavior remains unchanged. Tick releases the replaced connection's
old room; the new connection can resume its saved stage in a fresh room without
paying another energy. Loader fallback itself is in-memory until normal saving;
the tests do not claim it is an atomic location write. A replacement still inside
the old room is relocated before disposal, but that defensive branch is not the
normal loader path exercised by the cold-reload fixture.

Release refuses occupied or replaced maps and is idempotent after success.
Release retires owned active combat receipts before removing their actors;
failed retirement retains the actor for retry. Unexpected actors still block disposal.
The coordinator exposes public start/resume, private story actions and exit
dialogue, and wraps Tick. Production does not initialize it or schedule its Tick yet.

## Validation

Isolated build and **1699 combined Trojan checks** pass, including **1433 Trojan
assertions** with storage, item and journal coverage retained. The fixture visits
all three actual layouts, walks to every NPC and the reserved combat tile, checks
foreign entry through the real teleport handler, duplicate allocation, occupied
release refusal, saved arrival, normal return, expiry, cold reload, fresh-room
resume and unchanged story revisions. The separate Warrior Epic suite passes
**458 checks** after the shared Entity teleport guards were added.

These are ordinary-account server fixtures, not genuine-client rendering,
combat balance, capacity stress or complete earning acceptance. No live server,
client configuration or deployment artifact was changed.

## Owned combat

Ghost Reaver, Evil Monk Misery and Furious Flame Devastator use catalog models
979, 981 and 978. Private families 9916801-9916803 are separate from Ancient
Devil's computed range 9916501-9916505. The first attempted IDs overlapped that
range; actual CanAttack validation caught the conflict before publication.

HP is 150,000 / 350,000 / 1,000,000 and raw attack ranges are 800-1,200 /
1,200-1,800 / 1,800-2,500 respectively. These are explicit Revora tuning, not
verified retail stats or promised damage after player defenses. The latter two
use the existing boss HUD serializer; genuine-client appearance remains untested.

Only the registered owner or their registered companion can fight an encounter.
Damage and victory use the owner's item lock. Durable, revision-specific victory
receipts advance the story once; generic loot is suppressed. Canceling or leaving
a live battle retires its receipt without spending energy or advancing the story.

The fixture now defeats all eighteen reavers, the traitor and the Devastator
through ReceiveAttack. It verifies partial damage, outsider rejection, stale
death callbacks, trade suspension, failed durable cancellation, live-room exit,
and no incidental loot/currency. Monster AI balance remains untested; the full
NPC earning route is covered by the subsequently expanded fixture below.

## Private NPC story route

`TrojanEpicQuest` connects all six private-room actor IDs to their stored stages.
General Pak handles arrival/reports, essence gathering, borrowing and the final
exchange; Monk Misery handles array work and the traitor challenge; Mr. Mirror
opens Reaver encounters; Divine Jade infuses the essence. General Pak offers
guarded transfers between room parts once the story reaches the next phase.
These deterministic daily actions use the previously documented Revora energy
contract, not a newly inferred retail minigame.

Each mutating dialogue choice requires a server-issued offer tied to the exact
scene, NPC and saved revision. Offers expire in two minutes and are consumed
before execution, including when daily energy is exhausted. Proximity, canonical
session, eligibility and ownership are rechecked. Stale choices cannot be replayed
after daily renewal. The installed 2032 dispatcher applies the existing strict
frame checks; no new wire fields or protocol layout are introduced.

The ordinary-account fixture now uses NPC opening dispatch, sealed dialogue
replies, walking and real combat from admission through reward claim. It proves
all ninety energy receipts, twenty victories, atomic blade exchange, maximum
unbound saber persistence, duplicate rejection and working exit. It also rejects
unoffered choices, mismatched NPC echoes and short dialogue frames. This is server
earning acceptance; native journal navigation and real-client acceptance remain open. Candidate
startup/scheduling are now connected as described below.

## Public start and resume

Pak's Ghost (10579, mesh 29196) stands at Twin City 425,298; Super Mok
(10580, mesh 29206) stands at 466,400. These positions come from the installed
journal, and npc.ini profiles 2919 and 2920 confirm the names/models (input hash
in the research baseline). The fixture reads actual nearby public NPC catalog
rows and uses the real server terrain. Both footprints and walking approaches
are clear in that evidence. Placement refuses conflicting identities, nearby
NPC footprints or blocked ground before adding either actor; a failed addition
removes only this component's own additions. It writes no public NPC rows.

Pak starts the story and reforges the blade after twenty-seven daily scrap
actions at Super Mok. He then opens or resumes the private memory at its saved
stage. Reconnect uses the existing Twin City loader fallback; after the old room
is cleaned up, Pak allocates a fresh room without spending energy. Before cleanup,
the request asks the player to retry and leaves the character usable. Room
capacity uses the same nonfatal allocation path; full-capacity stress is now covered below. Public choices have the same revision/expiry protections as private ones,
plus exact registered actor identity. The fixture rejects replacement actors,
expired offers, ineligible characters and next-day replay of old scrap requests.

This route has no CP/silver shortcut. The old entrance stays closed. Trojan Star's
old travel/learning branches are replaced by [owned referral and earned teaching](TROJAN_EPIC_TRAINING.md).

## Candidate lifecycle and capacity acceptance (2026-09-21)

`Thread.Initialize` now calls `TrojanEpicQuest.Initialize` alongside Warrior Epic,
and the world timer calls its `Tick`. Initialization validates the exact terrain
and monster catalog, initializes durable storage, places the two owned public
actors, and enables the trainer and journal. This uses the already tested ordinary
player route; there is no account/GM allowlist. The lifecycle connection is source
integration, not evidence that the live Host has restarted with this build.
Scene recovery errors now identify `trojan-scene-recovery` instead of the copied
Warrior label.

The added capacity fixture earns the first 27 energy receipts and reforging for
34 ordinary characters through storage actions, then uses actual public NPC
opening/reply dispatch to enter. Exactly 32 receive distinct owned rooms. The two
waiting characters remain unfenced in Twin City with unchanged stage/energy.
After one owner exits, concurrent public retries allocate exactly that one slot;
the losing contender remains usable. Final cleanup removes every allocated map
and map-information entry. No capacity records or quest steps are fabricated;
this focused stress fixture does not repeat the full 90-energy NPC journey for
every contender. The pre-existing fixture still earns that complete NPC/combat
journey and reloads its maximum unbound saber.

Validation: isolated Debug/x86 build and 1,776 combined checks pass (1,510 Trojan
assertions plus 266 promotion prerequisites, including movement checks). The
achievement producer audit remains 240/337. Full Host startup/shutdown/restart and
genuine DX9 presentation remain pending. No live database, client, public download
or running server changed.

## Earned route across actual game processes (2026-09-27)

The earlier full-process gap is now covered by
`tools/Verify-ArenaProcess.ps1 -TrojanOnly` and
`tools/CheckCandidateTrojanLifecycle.cs`. The same private candidate executes
three separate game processes against an ownership-marked disposable database:

1. A supplied level-120 ordinary Trojan starts with no CPs or items. Pak's Ghost
   and Super Mok dialogs earn the 27 scrap actions, reforge the blade and admit
   the character to its owned memory. After saving that state, the probe exits
   the process immediately without the graceful scene-return path.
2. After both database and game restart, character loading returns it to Twin
   City. Stage 3 and energy 27 survive. Pak admits it to a new private memory
   without advancing the saved revision. Normal NPC handlers and controlled
   combat complete the array, 18 Reavers, traitor, essence/infusion, borrowed
   blade and Devastator route. The run ends at stage 14, energy 90 and 20 actual
   encounter victories. The issued Solar Blade is exchanged for exactly one
   permanent unbound maximum saber 614439, +12, -7, two empty sockets. Replaying
   the claim grants nothing; the exit reaches Trojan Star's map 1004.
3. Another game process reloads completed count 1, energy 90 and the exact saber
   UID and attributes. No borrowed blade remains. CPs stay zero and silver stays
   at the character's initial 100,000 throughout the quest.

All three phases passed on the existing candidate without production code changes
or another server build. Executable SHA-256:
`2CE8DF4B6824E702D03B821765B8E2AF105986C294841A02C2B224D9058BC944`.
The parent log is ignored `obj/trojan-process.log`; detailed redacted logs are
`arena-process-trojan-{capture,finish,verify}.log` in the private candidate root.
After success the marked clone was removed and private PostgreSQL stopped.

Scope: level/class qualification is supplied, daily progression advances only the
quest's injected clock, NPC proximity is controlled, and encounters receive
controlled lethal damage through the normal combat handler. Scene lifetime uses
its normal clock. Quest progress, energy, encounters and final rewards are earned
through production handlers, not inserted as completed fixture rows. This is not
a 90-day endurance test, combat balance test or authenticated native-client run.
The finish log also contains an unrelated vote-pulse PostgreSQL exception; this
quest result does not certify voting or overall host health.

Genuine DX9 journal navigation, NPC/weapon rendering, authenticated play and live
deployment remain pending. The existing twelve-entry private journal overlay and
its signing/installation gates are unchanged.

### Borrowed-blade and active final-trial restart

The same suite now runs **four processes**, extending the three-process baseline
above. After earning all 90 energy actions and the first 19 victories, General
Pak lends the Solar Blade and starts the Devastator encounter. The probe records
the loan UID and active encounter ID, then deliberately exits without graceful
scene cleanup. It restarts both PostgreSQL and the game before continuing.

The resumed ordinary character returns to Twin City at stage 12, energy 90, with
the **same bound Solar Blade UID** and no permanent saber. Re-entry preserves the
revision. Starting the replacement trial retires the exact interrupted encounter;
victory and Pak's exchange then consume the loan and issue one maximum saber.
Another process verifies the exact saber UID/attributes and unchanged currency.
It also reads **90 energy rows, 20 victory rows, one final reward receipt and one
returned loan record matching the original blade UID**. Claim replay still grants
nothing. No quest-completion rows were supplied by the fixture.

All four phases pass in `obj/trojan-blade-process.log` on the same executable
hash. The new redacted phase log is `arena-process-trojan-battle.log`. The owned
clone and private PostgreSQL were cleaned up after success. This follow-up changed
only the harness and documentation; the preceding qualification, controlled-clock,
combat, native-client and live-deployment limits still apply.
